using CliWrap;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class PandocConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly ILogger<PandocConverter> _logger;

    private static readonly HashSet<string> PandocInputSupported = new(StringComparer.OrdinalIgnoreCase)
    {
        "md", "markdown", "rst", "org", "latex", "tex", "epub", "html", "docx", "odt", "txt"
    };

    private static readonly HashSet<string> PandocOutputSupported = new(StringComparer.OrdinalIgnoreCase)
    {
        "md", "markdown", "rst", "org", "latex", "tex", "epub", "html", "docx", "odt", "txt", "pdf"
    };

    public PandocConverter(IBinaryLocator binaryLocator, ILogger<PandocConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    public string Name => "Pandoc Universal Document Converter";
    public int Priority => 90;

    public bool IsAvailable() => _binaryLocator.IsBinaryAvailable("pandoc");

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        return PandocInputSupported.Contains(src) && PandocOutputSupported.Contains(tgt);
    }

    public async Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        string outputDir, 
        IProgress<int>? progress = null, 
        CancellationToken ct = default)
    {
        var pandocExe = _binaryLocator.GetBinaryPathOrThrow("pandoc");
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");

        Directory.CreateDirectory(outputDir);
        progress?.Report(20);

        _logger.LogInformation("Executing Pandoc conversion: {Source} -> {Target}", sourcePath, outputPath);

        var args = $"-s \"{sourcePath}\" -o \"{outputPath}\"";

        await CliWrap.Cli.Wrap(pandocExe)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync(ct);

        progress?.Report(100);

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException($"Pandoc did not produce expected output at '{outputPath}'.");
        }

        return outputPath;
    }
}
