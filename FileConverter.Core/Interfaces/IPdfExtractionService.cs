namespace FileConverter.Core.Interfaces;

public interface IPdfExtractionService
{
    Task<string> ConvertPdfToWordAsync(string pdfPath, string outputDir, CancellationToken ct = default);
    Task<string> ConvertPdfToTextAsync(string pdfPath, string outputDir, CancellationToken ct = default);
    Task<List<string>> ExtractImagesFromPdfAsync(string pdfPath, string outputDir, CancellationToken ct = default);
}
