using System.Drawing;
using System.Drawing.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConverter.Core.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace FileConverter.Infrastructure.Services;

public class PdfConversionService : IPdfConversionService
{
    private readonly IFileSystemService _fileSystemService;

    public PdfConversionService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public async Task<string> ConvertImageToPdfAsync(string imagePath, string outputDir, CancellationToken ct = default)
    {
        var baseName = Path.GetFileNameWithoutExtension(imagePath);
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".pdf");
        return await ConvertImagesToSinglePdfAsync(new[] { imagePath }, dest, ct);
    }

    public Task<string> ConvertImagesToSinglePdfAsync(IEnumerable<string> imagePaths, string outputPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var files = imagePaths.ToList();
            if (files.Count == 0)
            {
                throw new ArgumentException("No images provided for PDF conversion.");
            }

            var dir = Path.GetDirectoryName(outputPath) ?? _fileSystemService.GetDefaultOutputDirectory();
            _fileSystemService.EnsureDirectoryExists(dir);

            using var pdfDoc = new PdfDocument();

            foreach (var imgPath in files)
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(imgPath)) continue;

                // Load with System.Drawing to read dimensions and normalize
                using var sysImage = System.Drawing.Image.FromFile(imgPath);
                using var ms = new MemoryStream();
                sysImage.Save(ms, ImageFormat.Png);
                var imgBytes = ms.ToArray();

                var page = pdfDoc.AddPage();
                
                // Keep image resolution sharp: calculate target dimensions
                double targetWidth = sysImage.Width * 72.0 / (sysImage.HorizontalResolution > 0 ? sysImage.HorizontalResolution : 96.0);
                double targetHeight = sysImage.Height * 72.0 / (sysImage.VerticalResolution > 0 ? sysImage.VerticalResolution : 96.0);

                // Constrain max page size if overly large
                const double maxDim = 2000.0;
                if (targetWidth > maxDim || targetHeight > maxDim)
                {
                    double ratio = Math.Min(maxDim / targetWidth, maxDim / targetHeight);
                    targetWidth *= ratio;
                    targetHeight *= ratio;
                }

                page.Width = XUnit.FromPoint(targetWidth);
                page.Height = XUnit.FromPoint(targetHeight);

                using var gfx = XGraphics.FromPdfPage(page);
                using var xImage = XImage.FromStream(() => new MemoryStream(imgBytes));
                gfx.DrawImage(xImage, 0, 0, targetWidth, targetHeight);
            }

            pdfDoc.Save(outputPath);
            return outputPath;
        }, ct);
    }

    public Task<string> ConvertTextToPdfAsync(string textPath, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(textPath))
            {
                throw new FileNotFoundException("Text file not found", textPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(textPath);
            var destination = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".pdf");

            var lines = File.ReadAllLines(textPath);

            using var pdfDoc = new PdfDocument();
            var font = new XFont("Arial", 10, XFontStyle.Regular);
            var headerFont = new XFont("Arial", 12, XFontStyle.Bold);

            const double margin = 40;
            const double lineHeight = 14;

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

                // Draw header
                gfx.DrawString($"{baseName} - Page {pageNumber}", headerFont, XBrushes.DarkSlateGray, margin, margin - 15);
                gfx.DrawLine(XPens.LightGray, margin, margin - 5, currentPage.Width - margin, margin - 5);
                currentY = margin + 15;
            }

            StartNewPage();

            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();
                var text = line ?? string.Empty;

                var words = text.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    var size = gfx!.MeasureString(testLine, font);

                    if (size.Width > (currentPage!.Width - (margin * 2)))
                    {
                        if (currentY + lineHeight > currentPage.Height - margin)
                        {
                            StartNewPage();
                        }
                        gfx.DrawString(currentLine, font, XBrushes.Black, margin, currentY);
                        currentY += lineHeight;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY + lineHeight > currentPage!.Height - margin)
                    {
                        StartNewPage();
                    }
                    gfx!.DrawString(currentLine, font, XBrushes.Black, margin, currentY);
                    currentY += lineHeight;
                }
            }

            gfx?.Dispose();
            pdfDoc.Save(destination);
            return destination;
        }, ct);
    }

    public Task<string> ConvertWordToPdfAsync(string docxPath, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(docxPath))
            {
                throw new FileNotFoundException("Word document not found", docxPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(docxPath);
            var destination = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".pdf");

            var extractedParagraphs = new List<(string text, bool isHeading)>();
            try
            {
                using var wordDoc = WordprocessingDocument.Open(docxPath, false);
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    foreach (var para in body.Elements<Paragraph>())
                    {
                        var text = para.InnerText;
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                        bool isHeading = styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ||
                                         styleId.Equals("Title", StringComparison.OrdinalIgnoreCase);

                        extractedParagraphs.Add((text, isHeading));
                    }
                }
            }
            catch
            {
                extractedParagraphs = ExtractTextFromLegacyDoc(docxPath);
            }

            using var pdfDoc = new PdfDocument();
            var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
            var headingFont = new XFont("Arial", 14, XFontStyle.Bold);
            var docHeaderFont = new XFont("Arial", 9, XFontStyle.Italic);

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

                gfx.DrawString($"{baseName} (Converted from Word)", docHeaderFont, XBrushes.Gray, margin, margin - 15);
                gfx.DrawLine(XPens.LightGray, margin, margin - 8, currentPage.Width - margin, margin - 8);
                currentY = margin + 15;
            }

            StartNewPage();

            foreach (var item in extractedParagraphs)
            {
                ct.ThrowIfCancellationRequested();
                var activeFont = item.isHeading ? headingFont : bodyFont;
                var brush = item.isHeading ? XBrushes.Navy : XBrushes.Black;
                double lineHeight = item.isHeading ? 20 : 14;

                var words = item.text.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    var size = gfx!.MeasureString(testLine, activeFont);

                    if (size.Width > (currentPage!.Width - (margin * 2)))
                    {
                        if (currentY + lineHeight > currentPage.Height - margin)
                        {
                            StartNewPage();
                        }
                        gfx.DrawString(currentLine, activeFont, brush, margin, currentY);
                        currentY += lineHeight;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY + lineHeight > currentPage!.Height - margin)
                    {
                        StartNewPage();
                    }
                    gfx!.DrawString(currentLine, activeFont, brush, margin, currentY);
                    currentY += lineHeight + (item.isHeading ? 6 : 4);
                }
            }

            gfx?.Dispose();
            pdfDoc.Save(destination);
            return destination;
        }, ct);
    }

    private static List<(string text, bool isHeading)> ExtractTextFromLegacyDoc(string filePath)
    {
        var result = new List<(string text, bool isHeading)>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var sb = new System.Text.StringBuilder();
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
}
