namespace FileConverter.Core.Interfaces;

public interface IPdfConversionService
{
    Task<string> ConvertImageToPdfAsync(string imagePath, string outputDir, CancellationToken ct = default);
    Task<string> ConvertImagesToSinglePdfAsync(IEnumerable<string> imagePaths, string outputPath, CancellationToken ct = default);
    Task<string> ConvertTextToPdfAsync(string textPath, string outputDir, CancellationToken ct = default);
    Task<string> ConvertWordToPdfAsync(string docxPath, string outputDir, CancellationToken ct = default);
}
