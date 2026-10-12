using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConverter.Core.Interfaces;
using UglyToad.PdfPig;

namespace FileConverter.Infrastructure.Services;

public class PdfExtractionService : IPdfExtractionService
{
    private readonly IFileSystemService _fileSystemService;

    public PdfExtractionService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public Task<string> ConvertPdfToWordAsync(string pdfPath, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("PDF file not found", pdfPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var destination = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".docx");

            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            using var wordDoc = WordprocessingDocument.Create(destination, WordprocessingDocumentType.Document);

            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            int pageCount = pdfDoc.NumberOfPages;
            for (int p = 1; p <= pageCount; p++)
            {
                ct.ThrowIfCancellationRequested();
                var page = pdfDoc.GetPage(p);
                var text = page.Text;

                // Add Page Header / Section Marker
                var pageHeading = new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading2" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = "3B82F6" }),
                        new Text($"--- Page {p} ---")
                    )
                );
                body.AppendChild(pageHeading);

                // Split page text into lines or paragraphs
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var para = new Paragraph(
                        new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
                        new Run(new Text(line))
                    );
                    body.AppendChild(para);
                }

                // If not last page, insert page break
                if (p < pageCount)
                {
                    var breakPara = new Paragraph(new Run(new Break { Type = BreakValues.Page }));
                    body.AppendChild(breakPara);
                }
            }

            mainPart.Document.Save();
            return destination;
        }, ct);
    }

    public Task<string> ConvertPdfToTextAsync(string pdfPath, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("PDF file not found", pdfPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var destination = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, ".txt");

            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            using var writer = new StreamWriter(destination, false, System.Text.Encoding.UTF8);

            int pageCount = pdfDoc.NumberOfPages;
            for (int p = 1; p <= pageCount; p++)
            {
                ct.ThrowIfCancellationRequested();
                var page = pdfDoc.GetPage(p);
                writer.WriteLine($"=== PAGE {p} of {pageCount} ===");
                writer.WriteLine();
                writer.WriteLine(page.Text);
                writer.WriteLine();
            }

            return destination;
        }, ct);
    }

    public Task<List<string>> ExtractImagesFromPdfAsync(string pdfPath, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("PDF file not found", pdfPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var extractedFiles = new List<string>();

            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            int imageIndex = 1;

            foreach (var page in pdfDoc.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                foreach (var image in page.GetImages())
                {
                    try
                    {
                        var dest = _fileSystemService.GetUniqueDestinationFilePath(
                            outputDir, 
                            $"{baseName}_page{page.Number}_img{imageIndex}", 
                            ".png"
                        );

                        if (image.TryGetPng(out var pngBytes))
                        {
                            File.WriteAllBytes(dest, pngBytes);
                            extractedFiles.Add(dest);
                            imageIndex++;
                        }
                        else if (image.RawBytes.Length > 0)
                        {
                            File.WriteAllBytes(dest, image.RawBytes.ToArray());
                            extractedFiles.Add(dest);
                            imageIndex++;
                        }
                    }
                    catch
                    {
                        // Some proprietary filter types may not export directly; continue with next image
                    }
                }
            }

            return extractedFiles;
        }, ct);
    }
}
