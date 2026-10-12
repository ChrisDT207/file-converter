namespace FileConverter.Core.Interfaces;

public interface IImageConversionService
{
    Task<string> ConvertImageAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default);
}
