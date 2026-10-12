namespace FileConverter.Core.Interfaces;

public interface IDocumentConversionService
{
    Task<string> ConvertDocumentAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default);
}
