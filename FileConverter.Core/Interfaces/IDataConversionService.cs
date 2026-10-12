namespace FileConverter.Core.Interfaces;

public interface IDataConversionService
{
    Task<string> ConvertDataAsync(string sourcePath, string targetExt, string outputDir, CancellationToken ct = default);
}
