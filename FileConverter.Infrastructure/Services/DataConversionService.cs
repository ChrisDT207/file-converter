using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FileConverter.Core.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace FileConverter.Infrastructure.Services;

public class DataConversionService : IDataConversionService
{
    private readonly IFileSystemService _fileSystemService;

    public DataConversionService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public Task<string> ConvertDataAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source data file not found", sourcePath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var sourceExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
            var cleanTarget = targetExt.TrimStart('.').ToLowerInvariant();

            var destination = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, "." + cleanTarget);

            // Read source into tabular model (List<string> headers, List<List<string>> rows)
            var (headers, rows) = LoadTabularData(sourcePath, sourceExt);

            switch (cleanTarget)
            {
                case "json":
                    ExportToJson(headers, rows, destination);
                    break;

                case "csv":
                    ExportToCsv(headers, rows, destination);
                    break;

                case "xlsx":
                    ExportToExcel(headers, rows, destination);
                    break;

                case "xml":
                    ExportToXml(headers, rows, destination, baseName);
                    break;

                case "html":
                    ExportToHtml(headers, rows, destination, baseName);
                    break;

                case "pdf":
                    ExportToPdf(headers, rows, destination, baseName);
                    break;

                case "txt":
                    ExportToText(headers, rows, destination);
                    break;

                default:
                    throw new NotSupportedException($"Data target format '{targetExt}' is not supported.");
            }

            return destination;
        }, ct);
    }

    private static (List<string> Headers, List<List<string>> Rows) LoadTabularData(string filePath, string ext)
    {
        var headers = new List<string>();
        var rows = new List<List<string>>();

        if (ext == "csv")
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length > 0)
            {
                headers = ParseCsvLine(lines[0]);
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    rows.Add(ParseCsvLine(lines[i]));
                }
            }
        }
        else if (ext == "json")
        {
            var jsonText = File.ReadAllText(filePath, Encoding.UTF8);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var headerSet = new HashSet<string>();
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            headerSet.Add(prop.Name);
                        }
                    }
                }

                headers = headerSet.ToList();
                foreach (var item in root.EnumerateArray())
                {
                    var row = new List<string>();
                    foreach (var h in headers)
                    {
                        if (item.TryGetProperty(h, out var val))
                        {
                            row.Add(val.ToString());
                        }
                        else
                        {
                            row.Add("");
                        }
                    }
                    rows.Add(row);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                headers = new List<string> { "Property", "Value" };
                foreach (var prop in root.EnumerateObject())
                {
                    rows.Add(new List<string> { prop.Name, prop.Value.ToString() });
                }
            }
        }
        else if (ext == "xml")
        {
            var xdoc = XDocument.Load(filePath);
            var elements = xdoc.Root?.Elements().ToList() ?? new();
            var headerSet = new HashSet<string>();

            foreach (var elem in elements)
            {
                foreach (var child in elem.Elements())
                {
                    headerSet.Add(child.Name.LocalName);
                }
                foreach (var attr in elem.Attributes())
                {
                    headerSet.Add("@" + attr.Name.LocalName);
                }
            }

            headers = headerSet.ToList();
            if (headers.Count == 0) headers = new List<string> { "Element", "Value" };

            foreach (var elem in elements)
            {
                var row = new List<string>();
                foreach (var h in headers)
                {
                    if (h.StartsWith('@'))
                    {
                        row.Add(elem.Attribute(h[1..])?.Value ?? "");
                    }
                    else
                    {
                        row.Add(elem.Element(h)?.Value ?? "");
                    }
                }
                rows.Add(row);
            }
        }
        else if (ext is "xlsx" or "xls")
        {
            using var spreadsheet = SpreadsheetDocument.Open(filePath, false);
            var workbookPart = spreadsheet.WorkbookPart;
            if (workbookPart?.Workbook != null)
            {
                var sheet = workbookPart.Workbook.Descendants<Sheet>().FirstOrDefault();
                if (sheet?.Id?.Value != null)
                {
                    var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
                    var sheetData = worksheetPart.Worksheet?.Elements<SheetData>().FirstOrDefault();
                    var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

                    if (sheetData != null)
                    {
                        bool isFirst = true;
                    foreach (var row in sheetData.Elements<Row>())
                    {
                        var cellValues = new List<string>();
                        foreach (var cell in row.Elements<Cell>())
                        {
                            string val = cell.CellValue?.Text ?? "";
                            if (cell.DataType != null && cell.DataType == CellValues.SharedString && sharedStringTable != null)
                            {
                                if (int.TryParse(val, out int sId))
                                {
                                    val = sharedStringTable.ElementAt(sId).InnerText;
                                }
                            }
                            cellValues.Add(val);
                        }

                        if (isFirst)
                        {
                            headers = cellValues;
                            isFirst = false;
                        }
                        else
                        {
                            rows.Add(cellValues);
                        }
                    }
                }
            }
        }
    }

    return (headers, rows);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static void ExportToJson(List<string> headers, List<List<string>> rows, string destination)
    {
        var list = new List<Dictionary<string, string>>();
        foreach (var r in rows)
        {
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < headers.Count; i++)
            {
                var val = i < r.Count ? r[i] : "";
                dict[headers[i]] = val;
            }
            list.Add(dict);
        }

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(destination, json, Encoding.UTF8);
    }

    private static void ExportToCsv(List<string> headers, List<List<string>> rows, string destination)
    {
        using var sw = new StreamWriter(destination, false, Encoding.UTF8);
        sw.WriteLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var r in rows)
        {
            sw.WriteLine(string.Join(",", r.Select(EscapeCsv)));
        }
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            return $"\"{s.Replace("\"", "\"\"")}\"";
        }
        return s;
    }

    private static void ExportToExcel(List<string> headers, List<List<string>> rows, string destination)
    {
        using var spreadsheet = SpreadsheetDocument.Create(destination, SpreadsheetDocumentType.Workbook);
        var workbookPart = spreadsheet.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Data"
        });

        // Header Row
        var headerRow = new Row();
        foreach (var h in headers)
        {
            headerRow.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(h) });
        }
        sheetData.Append(headerRow);

        // Data Rows
        foreach (var r in rows)
        {
            var row = new Row();
            foreach (var val in r)
            {
                row.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(val ?? "") });
            }
            sheetData.Append(row);
        }

        workbookPart.Workbook.Save();
    }

    private static void ExportToXml(List<string> headers, List<List<string>> rows, string destination, string baseName)
    {
        var cleanRootName = "Dataset";
        var cleanRowName = "Record";
        var doc = new XDocument(new XElement(cleanRootName));

        foreach (var r in rows)
        {
            var rowElem = new XElement(cleanRowName);
            for (int i = 0; i < headers.Count; i++)
            {
                var colName = System.Text.RegularExpressions.Regex.Replace(headers[i], @"[^\w]", "_");
                if (char.IsDigit(colName[0])) colName = "Col_" + colName;
                var val = i < r.Count ? r[i] : "";
                rowElem.Add(new XElement(colName, val));
            }
            doc.Root!.Add(rowElem);
        }

        doc.Save(destination);
    }

    private static void ExportToHtml(List<string> headers, List<List<string>> rows, string destination, string baseName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine($"<title>{baseName} - Data Table</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 30px; background: #f8fafc; color: #0f172a; }");
        sb.AppendLine("h2 { margin-bottom: 20px; font-weight: 700; color: #1e293b; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; background: #fff; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); border-radius: 8px; overflow: hidden; }");
        sb.AppendLine("th, td { padding: 12px 16px; text-align: left; border-bottom: 1px solid #e2e8f0; font-size: 14px; }");
        sb.AppendLine("th { background: #2563eb; color: #fff; font-weight: 600; }");
        sb.AppendLine("tr:hover { background: #f1f5f9; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h2>{baseName}</h2>");
        sb.AppendLine("<table><thead><tr>");
        foreach (var h in headers) sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
        sb.AppendLine("</tr></thead><tbody>");
        foreach (var r in rows)
        {
            sb.AppendLine("<tr>");
            for (int i = 0; i < headers.Count; i++)
            {
                var val = i < r.Count ? r[i] : "";
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(val)}</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table></body></html>");

        File.WriteAllText(destination, sb.ToString(), Encoding.UTF8);
    }

    private static void ExportToPdf(List<string> headers, List<List<string>> rows, string destination, string baseName)
    {
        using var pdfDoc = new PdfDocument();
        var page = pdfDoc.AddPage();
        page.Orientation = PdfSharpCore.PageOrientation.Landscape;
        page.Size = PdfSharpCore.PageSize.A4;

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
        var thFont = new XFont("Arial", 9, XFontStyle.Bold);
        var tdFont = new XFont("Arial", 8, XFontStyle.Regular);

        double margin = 30;
        double y = margin;
        gfx.DrawString($"Data Table: {baseName} ({rows.Count} records)", titleFont, XBrushes.Navy, margin, y);
        y += 25;

        double availableWidth = page.Width - (margin * 2);
        int colCount = Math.Max(1, Math.Min(headers.Count, 8)); // Max 8 cols per page layout
        double colWidth = availableWidth / colCount;

        // Draw Header
        gfx.DrawRectangle(XBrushes.MidnightBlue, margin, y, availableWidth, 20);
        for (int c = 0; c < colCount; c++)
        {
            gfx.DrawString(headers[c], thFont, XBrushes.White, margin + (c * colWidth) + 4, y + 14);
        }
        y += 20;

        // Draw Rows
        foreach (var r in rows.Take(40)) // Draw up to 40 records on first sheet
        {
            if (y > page.Height - margin) break;
            gfx.DrawLine(XPens.LightGray, margin, y + 15, margin + availableWidth, y + 15);
            for (int c = 0; c < colCount; c++)
            {
                var val = c < r.Count ? r[c] : "";
                if (val.Length > 25) val = val[..22] + "...";
                gfx.DrawString(val, tdFont, XBrushes.Black, margin + (c * colWidth) + 4, y + 12);
            }
            y += 16;
        }

        pdfDoc.Save(destination);
    }

    private static void ExportToText(List<string> headers, List<List<string>> rows, string destination)
    {
        using var sw = new StreamWriter(destination, false, Encoding.UTF8);
        sw.WriteLine(string.Join("\t", headers));
        foreach (var r in rows)
        {
            sw.WriteLine(string.Join("\t", r));
        }
    }
}
