namespace FileConverter.Core.Models;

public class TargetOptionDto
{
    public string Extension { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "document", "image", "data", "web"
    public string Description { get; set; } = string.Empty;
}

public static class ConversionMatrix
{
    private static readonly Dictionary<string, List<TargetOptionDto>> Matrix = new(StringComparer.OrdinalIgnoreCase)
    {
        // === PDF ===
        ["pdf"] = new()
        {
            new() { Extension = "docx", Label = "Word (.docx)", Category = "document", Description = "Convert PDF text & structure into editable Word document" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Extract raw text per page into plain text" },
            new() { Extension = "md",   Label = "Markdown (.md)", Category = "document", Description = "Extract formatted markdown with headings" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Export pages into styled HTML web pages" },
            new() { Extension = "png",  Label = "PNG (.png)", Category = "image", Description = "Extract embedded images as high-res PNGs" },
            new() { Extension = "jpg",  Label = "JPEG (.jpg)", Category = "image", Description = "Extract images as JPEG files" },
            new() { Extension = "bmp",  Label = "Bitmap (.bmp)", Category = "image", Description = "Extract images as BMP files" }
        },

        // === WORD (.docx, .doc) ===
        ["docx"] = new()
        {
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Render formatted Word document into publication PDF" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Extract clean text paragraphs" },
            new() { Extension = "md",   Label = "Markdown (.md)", Category = "document", Description = "Convert headings, lists & text to Markdown" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Generate responsive HTML markup" }
        },
        ["doc"] = new()
        {
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Render Word document to PDF" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Extract plain text" }
        },

        // === MARKDOWN (.md) ===
        ["md"] = new()
        {
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Render Markdown to styled HTML page" },
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Render Markdown to styled PDF document" },
            new() { Extension = "docx", Label = "Word (.docx)", Category = "document", Description = "Convert Markdown headings & body to Word (.docx)" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Plain text export" }
        },

        // === PLAIN TEXT (.txt, .log) ===
        ["txt"] = new()
        {
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Format text into paginated A4 PDF" },
            new() { Extension = "docx", Label = "Word (.docx)", Category = "document", Description = "Create structured Word (.docx) document" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Convert to preformatted HTML page" },
            new() { Extension = "md",   Label = "Markdown (.md)", Category = "document", Description = "Convert to Markdown document" }
        },
        ["log"] = new()
        {
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Format log file into paginated PDF" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Clean text export" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "HTML log report" }
        },

        // === HTML (.html, .htm) ===
        ["html"] = new()
        {
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Render HTML layout to PDF" },
            new() { Extension = "docx", Label = "Word (.docx)", Category = "document", Description = "Convert HTML elements into Word (.docx)" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Strip HTML tags to plain text" },
            new() { Extension = "md",   Label = "Markdown (.md)", Category = "document", Description = "Convert HTML tags into Markdown" }
        },
        ["htm"] = new()
        {
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Render HTML to PDF" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Strip tags to plain text" }
        },

        // === IMAGES (PNG, JPG, WEBP, BMP, GIF, TIFF, ICO) ===
        ["png"] = GetImageTargets("png"),
        ["jpg"] = GetImageTargets("jpg"),
        ["jpeg"] = GetImageTargets("jpeg"),
        ["webp"] = GetImageTargets("webp"),
        ["bmp"] = GetImageTargets("bmp"),
        ["gif"] = GetImageTargets("gif"),
        ["tiff"] = GetImageTargets("tiff"),
        ["tif"] = GetImageTargets("tif"),
        ["ico"] = GetImageTargets("ico"),

        // === DATA: CSV (.csv) ===
        ["csv"] = new()
        {
            new() { Extension = "json", Label = "JSON (.json)", Category = "data", Description = "Convert table rows to JSON array of objects" },
            new() { Extension = "xlsx", Label = "Excel (.xlsx)", Category = "data", Description = "Create genuine Excel spreadsheet workbook" },
            new() { Extension = "xml",  Label = "XML (.xml)", Category = "data", Description = "Convert records into structured XML elements" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Generate styled HTML data table" },
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Render tabular grid into PDF document" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Convert into fixed-width text columns" }
        },

        // === DATA: JSON (.json) ===
        ["json"] = new()
        {
            new() { Extension = "csv",  Label = "CSV (.csv)", Category = "data", Description = "Flatten array of JSON objects into tabular CSV" },
            new() { Extension = "xlsx", Label = "Excel (.xlsx)", Category = "data", Description = "Export JSON dataset to Excel workbook" },
            new() { Extension = "xml",  Label = "XML (.xml)", Category = "data", Description = "Convert JSON object tree to XML" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Interactive HTML table & tree view" },
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Pretty-print formatted JSON to PDF" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Formatted JSON text" }
        },

        // === DATA: XML (.xml) ===
        ["xml"] = new()
        {
            new() { Extension = "json", Label = "JSON (.json)", Category = "data", Description = "Parse XML into JSON object representation" },
            new() { Extension = "csv",  Label = "CSV (.csv)", Category = "data", Description = "Extract recurring XML elements into CSV" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Convert XML to formatted HTML view" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Indented plain XML text" }
        },

        // === DATA: EXCEL (.xlsx, .xls) ===
        ["xlsx"] = new()
        {
            new() { Extension = "csv",  Label = "CSV (.csv)", Category = "data", Description = "Extract active sheet into comma-separated CSV" },
            new() { Extension = "json", Label = "JSON (.json)", Category = "data", Description = "Convert sheet rows into JSON array" },
            new() { Extension = "html", Label = "HTML (.html)", Category = "web", Description = "Generate HTML table from spreadsheet" },
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Print spreadsheet grid to PDF" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Tab-delimited text export" }
        },
        ["xls"] = new()
        {
            new() { Extension = "csv",  Label = "CSV (.csv)", Category = "data", Description = "Extract rows to CSV" },
            new() { Extension = "txt",  Label = "Text (.txt)", Category = "document", Description = "Tab-delimited text export" }
        }
    };

    private static List<TargetOptionDto> GetImageTargets(string current)
    {
        var all = new List<TargetOptionDto>
        {
            new() { Extension = "png",  Label = "PNG (.png)", Category = "image", Description = "Lossless transparent PNG image" },
            new() { Extension = "jpg",  Label = "JPEG (.jpg)", Category = "image", Description = "Compressed high-compatibility JPEG image" },
            new() { Extension = "webp", Label = "WebP (.webp)", Category = "image", Description = "Next-gen web optimized image" },
            new() { Extension = "bmp",  Label = "Bitmap (.bmp)", Category = "image", Description = "Standard uncompressed Windows bitmap" },
            new() { Extension = "gif",  Label = "GIF (.gif)", Category = "image", Description = "Indexed color GIF graphics" },
            new() { Extension = "tiff", Label = "TIFF (.tiff)", Category = "image", Description = "High-precision photographic TIFF image" },
            new() { Extension = "ico",  Label = "Icon (.ico)", Category = "image", Description = "Windows application & website favicon icon" },
            new() { Extension = "pdf",  Label = "PDF (.pdf)", Category = "document", Description = "Embed image into standard PDF page" }
        };

        // Filter out the current extension itself
        return all.Where(t => !t.Extension.Equals(current, StringComparison.OrdinalIgnoreCase) && 
                              !(current == "jpeg" && t.Extension == "jpg")).ToList();
    }

    public static List<TargetOptionDto> GetAvailableTargetsForExtension(string ext)
    {
        var clean = ext.TrimStart('.').ToLowerInvariant();
        if (Matrix.TryGetValue(clean, out var targets))
        {
            return targets;
        }
        return new List<TargetOptionDto>();
    }

    public static Dictionary<string, List<TargetOptionDto>> GetAllMatrix()
    {
        return Matrix;
    }
}
