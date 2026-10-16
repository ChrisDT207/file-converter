using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using D = DocumentFormat.OpenXml.Drawing;

namespace FileConverter.Infrastructure.Services;

public class PresentationConversionService : IPresentationConversionService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<PresentationConversionService> _logger;

    public PresentationConversionService(
        IFileSystemService fileSystemService,
        ILogger<PresentationConversionService> logger)
    {
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public async Task<string> ConvertPresentationAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default)
    {
        var cleanTarget = targetExt.TrimStart('.').ToLowerInvariant();
        var sourceExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        _fileSystemService.EnsureDirectoryExists(outputDir);

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var slidesData = ExtractSlides(sourcePath);

            switch (cleanTarget)
            {
                case "pdf":
                    return ExportToPdf(slidesData, outputDir, baseName);

                case "txt":
                    return ExportToText(slidesData, outputDir, baseName);

                case "md":
                    return ExportToMarkdown(slidesData, outputDir, baseName);

                case "html":
                    return ExportToHtml(slidesData, outputDir, baseName);

                case "png" or "jpg" or "jpeg":
                    return ExportToImages(slidesData, outputDir, baseName, cleanTarget);

                default:
                    return ExportToPdf(slidesData, outputDir, baseName);
            }
        }, ct);
    }

    private List<SlideModel> ExtractSlides(string sourcePath)
    {
        var slidesList = new List<SlideModel>();

        try
        {
            using var presDoc = PresentationDocument.Open(sourcePath, false);
            var presPart = presDoc.PresentationPart;
            if (presPart == null) return FallbackExtractFromBinary(sourcePath);

            var slideParts = new List<SlidePart>();
            if (presPart.Presentation?.SlideIdList != null)
            {
                foreach (var slideId in presPart.Presentation.SlideIdList.Elements<SlideId>())
                {
                    var rId = slideId.RelationshipId?.Value;
                    if (!string.IsNullOrEmpty(rId) && presPart.GetPartById(rId) is SlidePart sp)
                    {
                        slideParts.Add(sp);
                    }
                }
            }

            if (slideParts.Count == 0)
            {
                slideParts = presPart.SlideParts.ToList();
            }

            int index = 1;
            foreach (var sp in slideParts)
            {
                var model = new SlideModel { SlideNumber = index++ };
                var slide = sp.Slide;
                if (slide == null) continue;

                // Extract all shapes
                foreach (var shape in slide.Descendants<Shape>())
                {
                    var isTitle = false;
                    var ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    if (ph != null && ph.Type != null)
                    {
                        var phType = ph.Type.Value;
                        if (phType == PlaceholderValues.Title || phType == PlaceholderValues.CenteredTitle)
                        {
                            isTitle = true;
                        }
                    }

                    if (shape.TextBody != null)
                    {
                        foreach (var para in shape.TextBody.Elements<D.Paragraph>())
                        {
                            var text = string.Concat(para.Descendants<D.Text>().Select(t => t.Text)).Trim();
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            if (isTitle && string.IsNullOrEmpty(model.Title))
                            {
                                model.Title = text;
                            }
                            else
                            {
                                model.BulletPoints.Add(text);
                            }
                        }
                    }
                }

                // If title was not found via placeholder, pick first short bullet
                if (string.IsNullOrEmpty(model.Title) && model.BulletPoints.Count > 0)
                {
                    model.Title = model.BulletPoints[0];
                    model.BulletPoints.RemoveAt(0);
                }

                // Extract embedded images from slide
                foreach (var imgPart in sp.ImageParts)
                {
                    try
                    {
                        using var s = imgPart.GetStream();
                        using var ms = new MemoryStream();
                        s.CopyTo(ms);
                        model.EmbeddedImages.Add(ms.ToArray());
                    }
                    catch { }
                }

                slidesList.Add(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenXML failed to parse presentation {File}. Using binary scraper fallback.", sourcePath);
            return FallbackExtractFromBinary(sourcePath);
        }

        if (slidesList.Count == 0)
        {
            return FallbackExtractFromBinary(sourcePath);
        }

        return slidesList;
    }

    private static List<SlideModel> FallbackExtractFromBinary(string filePath)
    {
        var list = new List<SlideModel>();
        var bytes = File.ReadAllBytes(filePath);
        var sb = new StringBuilder();
        var lines = new List<string>();

        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b >= 32 && b <= 126)
            {
                sb.Append((char)b);
            }
            else if (b == '\r' || b == '\n')
            {
                if (sb.Length > 3)
                {
                    lines.Add(sb.ToString().Trim());
                }
                sb.Clear();
            }
            else
            {
                if (sb.Length > 15)
                {
                    lines.Add(sb.ToString().Trim());
                }
                sb.Clear();
            }
        }

        var distinctLines = lines
            .Where(l => l.Length > 2 && !l.Contains("http://") && !l.Contains("schemas.openxml"))
            .Distinct()
            .ToList();

        var slide = new SlideModel
        {
            SlideNumber = 1,
            Title = distinctLines.Count > 0 ? distinctLines[0] : Path.GetFileNameWithoutExtension(filePath),
            BulletPoints = distinctLines.Skip(1).Take(15).ToList()
        };
        list.Add(slide);

        return list;
    }

    private string ExportToPdf(List<SlideModel> slides, string outputDir, string baseName)
    {
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".pdf");
        using var pdfDoc = new PdfDocument();

        var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 11, XFontStyle.Regular);
        var headerFont = new XFont("Arial", 9, XFontStyle.Bold);
        var footerFont = new XFont("Arial", 8, XFontStyle.Regular);

        var accentBrush = new XSolidBrush(XColor.FromArgb(99, 102, 241)); // Indigo accent
        var titleBrush = new XSolidBrush(XColor.FromArgb(15, 23, 42)); // Slate 900
        var bodyBrush = new XSolidBrush(XColor.FromArgb(51, 65, 85)); // Slate 700
        var mutedBrush = new XSolidBrush(XColor.FromArgb(148, 163, 184)); // Slate 400

        foreach (var slide in slides)
        {
            var page = pdfDoc.AddPage();
            page.Orientation = PageOrientation.Landscape;
            // A4 landscape = 842 x 595
            page.Width = XUnit.FromPoint(842);
            page.Height = XUnit.FromPoint(595);

            using var gfx = XGraphics.FromPdfPage(page);

            // 1. Top Decorative Bar
            gfx.DrawRectangle(accentBrush, 0, 0, page.Width, 8);

            // 2. Slide Number Badge
            var badgeText = $"SLIDE {slide.SlideNumber} OF {slides.Count}";
            gfx.DrawString(badgeText, headerFont, accentBrush, page.Width - 140, 36);

            // 3. Document Name Header
            gfx.DrawString(baseName, headerFont, mutedBrush, 50, 36);
            gfx.DrawLine(new XPen(XColor.FromArgb(226, 232, 240), 1), 50, 48, page.Width - 50, 48);

            // 4. Slide Title
            var title = string.IsNullOrWhiteSpace(slide.Title) ? $"Slide {slide.SlideNumber}" : slide.Title;
            gfx.DrawString(title, titleFont, titleBrush, 50, 85);

            // 5. Content Layout (Left Text, Right Image if available)
            double currentY = 120;
            double contentWidth = slide.EmbeddedImages.Count > 0 ? 460 : 740;

            foreach (var bp in slide.BulletPoints)
            {
                if (currentY > page.Height - 60) break;

                // Bullet point circle
                gfx.DrawEllipse(accentBrush, 52, currentY - 6, 4, 4);

                // Word wrap text
                var words = bp.Split(' ');
                var currentLine = "";

                foreach (var w in words)
                {
                    var test = string.IsNullOrEmpty(currentLine) ? w : $"{currentLine} {w}";
                    var size = gfx.MeasureString(test, bodyFont);

                    if (size.Width > contentWidth)
                    {
                        gfx.DrawString(currentLine, bodyFont, bodyBrush, 64, currentY);
                        currentY += 18;
                        currentLine = w;
                        if (currentY > page.Height - 60) break;
                    }
                    else
                    {
                        currentLine = test;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine) && currentY <= page.Height - 60)
                {
                    gfx.DrawString(currentLine, bodyFont, bodyBrush, 64, currentY);
                    currentY += 22;
                }
            }

            // 6. Embedded Image rendering (if available)
            if (slide.EmbeddedImages.Count > 0)
            {
                try
                {
                    using var imgStream = new MemoryStream(slide.EmbeddedImages[0]);
                    using var xImg = XImage.FromStream(() => new MemoryStream(slide.EmbeddedImages[0]));

                    double maxImgW = 240;
                    double maxImgH = 340;
                    double imgW = xImg.PixelWidth;
                    double imgH = xImg.PixelHeight;

                    double ratio = Math.Min(maxImgW / imgW, maxImgH / imgH);
                    imgW *= ratio;
                    imgH *= ratio;

                    double imgX = page.Width - 50 - imgW;
                    double imgY = 110;

                    gfx.DrawImage(xImg, imgX, imgY, imgW, imgH);
                }
                catch { }
            }

            // 7. Footer
            gfx.DrawLine(new XPen(XColor.FromArgb(241, 245, 249), 1), 50, page.Height - 32, page.Width - 50, page.Height - 32);
            gfx.DrawString("Converted by Universal File Converter", footerFont, mutedBrush, 50, page.Height - 18);
            gfx.DrawString($"Page {slide.SlideNumber}", footerFont, mutedBrush, page.Width - 90, page.Height - 18);
        }

        pdfDoc.Save(dest);
        return dest;
    }

    private string ExportToText(List<SlideModel> slides, string outputDir, string baseName)
    {
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".txt");
        var sb = new StringBuilder();
        sb.AppendLine($"# {baseName}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        foreach (var slide in slides)
        {
            sb.AppendLine($"=== SLIDE {slide.SlideNumber}: {slide.Title} ===");
            foreach (var bp in slide.BulletPoints)
            {
                sb.AppendLine($"  • {bp}");
            }
            sb.AppendLine();
        }

        File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
        return dest;
    }

    private string ExportToMarkdown(List<SlideModel> slides, string outputDir, string baseName)
    {
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".md");
        var sb = new StringBuilder();
        sb.AppendLine($"# {baseName}\n");

        foreach (var slide in slides)
        {
            sb.AppendLine($"## Slide {slide.SlideNumber}: {slide.Title}\n");
            foreach (var bp in slide.BulletPoints)
            {
                sb.AppendLine($"- {bp}");
            }
            sb.AppendLine("\n---\n");
        }

        File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
        return dest;
    }

    private string ExportToHtml(List<SlideModel> slides, string outputDir, string baseName)
    {
        var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".html");
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'>");
        sb.AppendLine($"<title>{System.Net.WebUtility.HtmlEncode(baseName)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; margin: 0; padding: 40px 20px; }");
        sb.AppendLine(".container { max-width: 900px; margin: 0 auto; }");
        sb.AppendLine(".pres-header { text-align: center; margin-bottom: 40px; }");
        sb.AppendLine(".pres-header h1 { font-size: 2.2rem; color: #fff; margin-bottom: 8px; }");
        sb.AppendLine(".slide-card { background: #1e293b; border-radius: 12px; padding: 32px; margin-bottom: 30px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.3); border: 1px solid #334155; }");
        sb.AppendLine(".slide-badge { display: inline-block; padding: 4px 10px; background: #6366f1; color: #fff; border-radius: 9999px; font-size: 0.75rem; font-weight: 600; text-transform: uppercase; margin-bottom: 16px; }");
        sb.AppendLine(".slide-title { font-size: 1.4rem; color: #fff; margin-top: 0; margin-bottom: 20px; }");
        sb.AppendLine(".slide-list { list-style-type: disc; padding-left: 24px; line-height: 1.8; color: #cbd5e1; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine($"<div class='pres-header'><h1>{System.Net.WebUtility.HtmlEncode(baseName)}</h1><p style='color:#94a3b8;'>Presentation Document</p></div>");

        foreach (var slide in slides)
        {
            sb.AppendLine("<div class='slide-card'>");
            sb.AppendLine($"<span class='slide-badge'>Slide {slide.SlideNumber}</span>");
            sb.AppendLine($"<h2 class='slide-title'>{System.Net.WebUtility.HtmlEncode(slide.Title)}</h2>");
            if (slide.BulletPoints.Count > 0)
            {
                sb.AppendLine("<ul class='slide-list'>");
                foreach (var bp in slide.BulletPoints)
                {
                    sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(bp)}</li>");
                }
                sb.AppendLine("</ul>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div></body></html>");
        File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
        return dest;
    }

    private string ExportToImages(List<SlideModel> slides, string outputDir, string baseName, string format)
    {
        // If embedded images exist, save the first one
        foreach (var slide in slides)
        {
            if (slide.EmbeddedImages.Count > 0)
            {
                var imgDest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, $"{baseName}_image", $".{format}");
                File.WriteAllBytes(imgDest, slide.EmbeddedImages[0]);
                return imgDest;
            }
        }

        // Otherwise generate a high-res slide preview via PDF
        var pdfPath = ExportToPdf(slides, outputDir, baseName);
        return pdfPath;
    }

    private class SlideModel
    {
        public int SlideNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<string> BulletPoints { get; set; } = new();
        public List<byte[]> EmbeddedImages { get; set; } = new();
    }
}
