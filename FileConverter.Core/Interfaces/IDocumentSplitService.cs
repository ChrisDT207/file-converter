namespace FileConverter.Core.Interfaces;

public interface IDocumentSplitService
{
    Task<List<string>> SplitPdfToSinglePagesAsync(string pdfPath, string outputDir, CancellationToken ct = default);
    Task<List<string>> SplitPdfByRangesAsync(string pdfPath, string outputDir, string rangeExpression, CancellationToken ct = default);
}
