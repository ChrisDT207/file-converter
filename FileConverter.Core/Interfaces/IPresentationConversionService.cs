namespace FileConverter.Core.Interfaces;

public interface IPresentationConversionService
{
    Task<string> ConvertPresentationAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default);
}
