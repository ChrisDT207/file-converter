namespace FileConverter.Core.Interfaces;

public interface IDocumentMergeService
{
    Task<string> MergePdfDocumentsAsync(IEnumerable<string> pdfPaths, string outputPath, CancellationToken ct = default);
}
