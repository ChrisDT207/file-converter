using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConverter.Core.Interfaces;
using Markdig;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FileConverter.Infrastructure.Services;

public class DocumentConversionService : IDocumentConversionService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IPdfConversionService _pdfConversionService;
    private readonly IPdfExtractionService _pdfExtractionService;

    public DocumentConversionService(
        IFileSystemService fileSystemService,
        IPdfConversionService pdfConversionService,
        IPdfExtractionService pdfExtractionService)
    {
        _fileSystemService = fileSystemService;
        _pdfConversionService = pdfConversionService;
        _pdfExtractionService = pdfExtractionService;
    }

    public async Task<string> ConvertDocumentAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default)
    {
        var cleanTarget = targetExt.TrimStart('.').ToLowerInvariant();
        var sourceExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        // 1. If Source is PDF
        if (sourceExt == "pdf")
        {
            if (cleanTarget == "docx")
            {
                return await _pdfExtractionService.ConvertPdfToWordAsync(sourcePath, outputDir, ct);
            }
            if (cleanTarget == "txt")
            {
                return await _pdfExtractionService.ConvertPdfToTextAsync(sourcePath, outputDir, ct);
            }
            if (cleanTarget is "png" or "jpg" or "jpeg" or "bmp")
            {
                return await RenderPdfToImageAsync(sourcePath, cleanTarget, outputDir, baseName, ct);
            }
            if (cleanTarget == "rtf")
            {
                var txt = await _pdfExtractionService.ConvertPdfToTextAsync(sourcePath, outputDir, ct);
                var raw = await File.ReadAllTextAsync(txt, ct);
                return ConvertTextToRtf(raw, outputDir, baseName);
            }
            if (cleanTarget == "md")
            {
                var txt = await _pdfExtractionService.ConvertPdfToTextAsync(sourcePath, outputDir, ct);
                var mdDest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".md");
                var rawLines = File.ReadAllLines(txt);
                var mdSb = new StringBuilder();
                mdSb.AppendLine($"# {baseName}\n");
                foreach (var line in rawLines)
                {
                    if (line.StartsWith("=== PAGE"))
                    {
                        mdSb.AppendLine($"\n## {line.Trim('=')}\n");
                    }
                    else
                    {
                        mdSb.AppendLine(line);
                    }
                }
                File.WriteAllText(mdDest, mdSb.ToString(), Encoding.UTF8);
                return mdDest;
            }
            if (cleanTarget == "html")
            {
                var txt = await _pdfExtractionService.ConvertPdfToTextAsync(sourcePath, outputDir, ct);
                var htmlDest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".html");
                var raw = File.ReadAllText(txt);
                var htmlContent = $"<!DOCTYPE html><html><head><meta charset='utf-8'><title>{baseName}</title>" +
                                  "<style>body{font-family:sans-serif;max-width:800px;margin:40px auto;padding:20px;line-height:1.6;color:#1e293b;}</style></head>" +
                                  $"<body><h1>{baseName}</h1><pre style='white-space:pre-wrap;font-family:inherit;'>{System.Net.WebUtility.HtmlEncode(raw)}</pre></body></html>";
                File.WriteAllText(htmlDest, htmlContent, Encoding.UTF8);
                return htmlDest;
            }
        }

        // 2. If Source is Word (.docx, .doc)
        if (sourceExt is "docx" or "doc")
        {
            if (cleanTarget == "pdf")
            {
                return await _pdfConversionService.ConvertWordToPdfAsync(sourcePath, outputDir, ct);
            }
            if (cleanTarget == "rtf")
            {
                var txt = await ConvertWordToFormatsAsync(sourcePath, "txt", outputDir, baseName, ct);
                var raw = await File.ReadAllTextAsync(txt, ct);
                return ConvertTextToRtf(raw, outputDir, baseName);
            }
            if (cleanTarget is "txt" or "md" or "html")
            {
                return await ConvertWordToFormatsAsync(sourcePath, cleanTarget, outputDir, baseName, ct);
            }
        }

        // 3. If Source is Markdown (.md)
        if (sourceExt == "md")
        {
            if (cleanTarget == "html")
            {
                return await ConvertMarkdownToHtmlAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "pdf")
            {
                return await ConvertMarkdownToPdfAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "docx")
            {
                return await ConvertMarkdownToWordAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "rtf")
            {
                var text = await File.ReadAllTextAsync(sourcePath, ct);
                return ConvertTextToRtf(text, outputDir, baseName);
            }
            if (cleanTarget == "txt")
            {
                var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".txt");
                File.Copy(sourcePath, dest, true);
                return dest;
            }
        }

        // 4. If Source is HTML (.html, .htm)
        if (sourceExt is "html" or "htm")
        {
            if (cleanTarget == "txt")
            {
                return await ConvertHtmlToTextAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "md")
            {
                return await ConvertHtmlToMarkdownAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "docx")
            {
                return await ConvertHtmlToWordAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "rtf")
            {
                var txt = await ConvertHtmlToTextAsync(sourcePath, outputDir, baseName, ct);
                var raw = await File.ReadAllTextAsync(txt, ct);
                return ConvertTextToRtf(raw, outputDir, baseName);
            }
            if (cleanTarget == "pdf")
            {
                return await _pdfConversionService.ConvertTextToPdfAsync(sourcePath, outputDir, ct);
            }
        }

        // 5. If Source is Plain Text (.txt, .log)
        if (sourceExt is "txt" or "log")
        {
            if (cleanTarget == "pdf")
            {
                return await _pdfConversionService.ConvertTextToPdfAsync(sourcePath, outputDir, ct);
            }
            if (cleanTarget == "docx")
            {
                return await ConvertTextToWordAsync(sourcePath, outputDir, baseName, ct);
            }
            if (cleanTarget == "rtf")
            {
                var text = await File.ReadAllTextAsync(sourcePath, ct);
                return ConvertTextToRtf(text, outputDir, baseName);
            }
            if (cleanTarget == "html")
            {
                var htmlDest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".html");
                var text = await File.ReadAllTextAsync(sourcePath, ct);
                var html = $"<!DOCTYPE html><html><head><meta charset='utf-8'><title>{baseName}</title>" +
                           "<style>body{font-family:sans-serif;padding:30px;line-height:1.6;background:#f8fafc;}</style></head>" +
                           $"<body><h2>{baseName}</h2><pre style='background:#fff;padding:20px;border-radius:8px;border:1px solid #e2e8f0;white-space:pre-wrap;'>{System.Net.WebUtility.HtmlEncode(text)}</pre></body></html>";
                await File.WriteAllTextAsync(htmlDest, html, Encoding.UTF8, ct);
                return htmlDest;
            }
            if (cleanTarget == "md")
            {
                var mdDest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".md");
                var text = await File.ReadAllTextAsync(sourcePath, ct);
                await File.WriteAllTextAsync(mdDest, $"# {baseName}\n\n```\n{text}\n```", Encoding.UTF8, ct);
                return mdDest;
            }
        }

        throw new NotSupportedException($"Conversion from '{sourceExt}' to '{targetExt}' is not supported.");
    }

    private static Task<string> ConvertWordToFormatsAsync(string sourcePath, string targetExt, string outputDir, string baseName, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var paragraphs = new List<(string text, bool isHeading)>();
            try
            {
                using var doc = WordprocessingDocument.Open(sourcePath, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    foreach (var p in body.Elements<Paragraph>())
                    {
                        var text = p.InnerText;
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                        bool isHeading = styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ||
                                         styleId.Equals("Title", StringComparison.OrdinalIgnoreCase);

                        paragraphs.Add((text, isHeading));
                    }
                }
            }
            catch
            {
                paragraphs = ExtractTextFromLegacyDoc(sourcePath);
            }

            var dest = Path.Combine(outputDir, $"{baseName}.{targetExt}");

            if (targetExt == "txt")
            {
                var sb = new StringBuilder();
                foreach (var (text, isHeading) in paragraphs)
                {
                    if (isHeading) sb.AppendLine($"=== {text} ===");
                    else sb.AppendLine(text);
                    sb.AppendLine();
                }
                File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
            }
            else if (targetExt == "md")
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# {baseName}\n");
                foreach (var (text, isHeading) in paragraphs)
                {
                    if (isHeading) sb.AppendLine($"## {text}\n");
                    else sb.AppendLine($"{text}\n");
                }
                File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
            }
            else if (targetExt == "html")
            {
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                sb.AppendLine($"<title>{baseName}</title>");
                sb.AppendLine("<style>body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;max-width:800px;margin:40px auto;padding:20px;line-height:1.7;color:#1e293b;} h1,h2{color:#0f172a;} p{margin-bottom:14px;}</style></head><body>");
                sb.AppendLine($"<h1>{baseName}</h1>");
                foreach (var (text, isHeading) in paragraphs)
                {
                    if (isHeading) sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(text)}</h2>");
                    else sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
                }
                sb.AppendLine("</body></html>");
                File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
            }

            return dest;
        }, ct);
    }

    private static async Task<string> ConvertMarkdownToHtmlAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        var mdText = await File.ReadAllTextAsync(sourcePath, ct);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var htmlBody = Markdown.ToHtml(mdText, pipeline);

        var dest = Path.Combine(outputDir, $"{baseName}.html");
        var fullHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{baseName}</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 840px; margin: 40px auto; padding: 24px; line-height: 1.7; color: #1e293b; background: #f8fafc; }}
        article {{ background: #ffffff; padding: 40px; border-radius: 12px; border: 1px solid #e2e8f0; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
        h1, h2, h3 {{ color: #0f172a; margin-top: 1.5em; margin-bottom: 0.5em; }}
        code {{ background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 0.9em; }}
        pre code {{ display: block; padding: 14px; overflow-x: auto; background: #0f172a; color: #f8fafc; border-radius: 8px; }}
        blockquote {{ border-left: 4px solid #3b82f6; margin: 1em 0; padding-left: 1em; color: #475569; }}
        table {{ width: 100%; border-collapse: collapse; margin: 1.5em 0; }}
        th, td {{ border: 1px solid #e2e8f0; padding: 8px 12px; }}
        th {{ background: #f8fafc; }}
    </style>
</head>
<body>
    <article>
        {htmlBody}
    </article>
</body>
</html>";

        await File.WriteAllTextAsync(dest, fullHtml, Encoding.UTF8, ct);
        return dest;
    }

    private async Task<string> ConvertMarkdownToPdfAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        // Convert to intermediate clean text layout for high-res PDF rendering
        var lines = await File.ReadAllLinesAsync(sourcePath, ct);
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".pdf");

        using var pdfDoc = new PdfDocument();
        var h1Font = new XFont("Arial", 16, XFontStyle.Bold);
        var h2Font = new XFont("Arial", 13, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
        var codeFont = new XFont("Courier New", 9, XFontStyle.Regular);

        const double margin = 45;
        PdfPage? currentPage = null;
        XGraphics? gfx = null;
        double currentY = margin;
        int pageNumber = 0;

        void StartNewPage()
        {
            gfx?.Dispose();
            currentPage = pdfDoc.AddPage();
            currentPage.Size = PdfSharpCore.PageSize.A4;
            gfx = XGraphics.FromPdfPage(currentPage);
            pageNumber++;
            currentY = margin;
        }

        StartNewPage();

        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            var text = line ?? "";

            XFont font = bodyFont;
            XBrush brush = XBrushes.Black;
            double spacing = 15;

            if (text.StartsWith("# "))
            {
                font = h1Font;
                brush = XBrushes.Navy;
                text = text[2..];
                spacing = 24;
            }
            else if (text.StartsWith("## "))
            {
                font = h2Font;
                brush = XBrushes.DarkSlateBlue;
                text = text[3..];
                spacing = 20;
            }
            else if (text.StartsWith("### "))
            {
                font = h2Font;
                brush = XBrushes.DarkSlateGray;
                text = text[4..];
                spacing = 18;
            }
            else if (text.StartsWith("```") || text.StartsWith("    "))
            {
                font = codeFont;
                brush = XBrushes.DarkSlateGray;
                spacing = 13;
            }

            if (currentY + spacing > currentPage!.Height - margin)
            {
                StartNewPage();
            }

            gfx!.DrawString(text, font, brush, margin, currentY);
            currentY += spacing;
        }

        gfx?.Dispose();
        pdfDoc.Save(dest);
        return dest;
    }

    private static async Task<string> ConvertMarkdownToWordAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(sourcePath, ct);
        var dest = Path.Combine(outputDir, $"{baseName}.docx");

        using var wordDoc = WordprocessingDocument.Create(dest, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string text = line;
            string styleId = "Normal";

            if (text.StartsWith("# "))
            {
                styleId = "Heading1";
                text = text[2..];
            }
            else if (text.StartsWith("## "))
            {
                styleId = "Heading2";
                text = text[3..];
            }
            else if (text.StartsWith("### "))
            {
                styleId = "Heading3";
                text = text[4..];
            }

            var para = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
                new Run(new Text(text))
            );
            body.AppendChild(para);
        }

        mainPart.Document.Save();
        return dest;
    }

    private static async Task<string> ConvertHtmlToTextAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        var html = await File.ReadAllTextAsync(sourcePath, ct);
        var cleanText = Regex.Replace(html, "<style[\\s\\S]*?</style>", "", RegexOptions.IgnoreCase);
        cleanText = Regex.Replace(cleanText, "<script[\\s\\S]*?</script>", "", RegexOptions.IgnoreCase);
        cleanText = Regex.Replace(cleanText, "<[^>]+>", "\n");
        cleanText = System.Net.WebUtility.HtmlDecode(cleanText);
        cleanText = Regex.Replace(cleanText, @"\n{3,}", "\n\n");

        var dest = Path.Combine(outputDir, $"{baseName}.txt");
        await File.WriteAllTextAsync(dest, cleanText.Trim(), Encoding.UTF8, ct);
        return dest;
    }

    private static async Task<string> ConvertHtmlToMarkdownAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        var html = await File.ReadAllTextAsync(sourcePath, ct);
        var md = html;
        md = Regex.Replace(md, "<h1[^>]*>(.*?)</h1>", "# $1\n\n", RegexOptions.IgnoreCase);
        md = Regex.Replace(md, "<h2[^>]*>(.*?)</h2>", "## $1\n\n", RegexOptions.IgnoreCase);
        md = Regex.Replace(md, "<h3[^>]*>(.*?)</h3>", "### $1\n\n", RegexOptions.IgnoreCase);
        md = Regex.Replace(md, "<p[^>]*>(.*?)</p>", "$1\n\n", RegexOptions.IgnoreCase);
        md = Regex.Replace(md, "<li[^>]*>(.*?)</li>", "- $1\n", RegexOptions.IgnoreCase);
        md = Regex.Replace(md, "<[^>]+>", "");
        md = System.Net.WebUtility.HtmlDecode(md);

        var dest = Path.Combine(outputDir, $"{baseName}.md");
        await File.WriteAllTextAsync(dest, md.Trim(), Encoding.UTF8, ct);
        return dest;
    }

    private static async Task<string> ConvertHtmlToWordAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        var txt = await ConvertHtmlToTextAsync(sourcePath, outputDir, baseName, ct);
        var lines = await File.ReadAllLinesAsync(txt, ct);
        var dest = Path.Combine(outputDir, $"{baseName}.docx");

        using var wordDoc = WordprocessingDocument.Create(dest, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var para = new Paragraph(new Run(new Text(line)));
            body.AppendChild(para);
        }

        mainPart.Document.Save();
        return dest;
    }

    private static async Task<string> ConvertTextToWordAsync(string sourcePath, string outputDir, string baseName, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(sourcePath, ct);
        var dest = Path.Combine(outputDir, $"{baseName}.docx");

        using var wordDoc = WordprocessingDocument.Create(dest, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        foreach (var line in lines)
        {
            var para = new Paragraph(new Run(new Text(line ?? "")));
            body.AppendChild(para);
        }

        mainPart.Document.Save();
        return dest;
    }

    private static List<(string text, bool isHeading)> ExtractTextFromLegacyDoc(string filePath)
    {
        var result = new List<(string text, bool isHeading)>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= 32 && b <= 126)
                {
                    sb.Append((char)b);
                }
                else if (b == '\r' || b == '\n' || b == '\t')
                {
                    if (sb.Length > 0)
                    {
                        var str = sb.ToString().Trim();
                        if (str.Length > 3)
                        {
                            result.Add((str, str.Length < 45 && !str.EndsWith('.')));
                        }
                        sb.Clear();
                    }
                }
                else
                {
                    if (sb.Length > 20)
                    {
                        var str = sb.ToString().Trim();
                        result.Add((str, false));
                    }
                    sb.Clear();
                }
            }
        }
        catch { }

        if (result.Count == 0)
        {
            result.Add(("[Legacy document content]", false));
        }
        return result;
    }

    private async Task<string> RenderPdfToImageAsync(string sourcePath, string cleanTarget, string outputDir, string baseName, CancellationToken ct)
    {
        var tgt = cleanTarget == "jpeg" ? "jpg" : cleanTarget;
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, "." + tgt);
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(sourcePath));
            var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(storageFile);
            if (pdfDoc.PageCount > 0)
            {
                using var page = pdfDoc.GetPage(0);
                using var stream = new InMemoryRandomAccessStream();

                var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                if (tgt == "jpg")
                {
                    options.BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId;
                }
                else if (tgt == "bmp")
                {
                    options.BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId;
                }
                else
                {
                    options.BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId;
                }

                await page.RenderToStreamAsync(stream, options);
                using var netStream = stream.AsStreamForRead();
                using var fileStream = File.Create(dest);
                await netStream.CopyToAsync(fileStream, ct);

                if (File.Exists(dest) && new FileInfo(dest).Length > 0)
                {
                    return dest;
                }
            }
        }
        catch
        {
            // Fallback to extraction
        }

        var extracted = await _pdfExtractionService.ExtractImagesFromPdfAsync(sourcePath, outputDir, ct);
        if (extracted.Count > 0) return extracted[0];
        throw new InvalidOperationException("Failed to render PDF page or extract images from PDF.");
    }

    private string ConvertTextToRtf(string text, string outputDir, string baseName)
    {
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".rtf");
        var sb = new StringBuilder();
        sb.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Calibri;}{\f1\fnil\fcharset0 Arial;}}");
        sb.AppendLine(@"{\colortbl ;\red30\green41\blue59;\red59\green130\blue246;}");
        sb.AppendLine(@"\viewkind4\uc1\pard\sa200\sl276\slmult1\f0\fs22\lang9 ");

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine(@"\par");
                continue;
            }

            var escaped = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '\\') escaped.Append(@"\\");
                else if (c == '{') escaped.Append(@"\{");
                else if (c == '}') escaped.Append(@"\}");
                else if (c > 127) escaped.Append(@"\u").Append((short)c).Append('?');
                else escaped.Append(c);
            }

            if (line.StartsWith('#'))
            {
                sb.AppendLine($@"\fs28\b {escaped}\b0\fs22\par");
            }
            else
            {
                sb.AppendLine($@"{escaped}\par");
            }
        }

        sb.AppendLine("}");
        File.WriteAllText(dest, sb.ToString(), Encoding.ASCII);
        return dest;
    }
}
