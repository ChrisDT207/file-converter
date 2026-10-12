namespace FileConverter.Core.Interfaces;

public interface IFileConverter
{
    string Name { get; }
    int Priority { get; } // Higher priority takes precedence (e.g. specialized CLI or managed)
    bool IsAvailable();
    bool CanConvert(string sourceExtension, string targetExtension);
    Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        string outputDir, 
        IProgress<int>? progress = null, 
        CancellationToken ct = default);
}
