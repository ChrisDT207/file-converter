using CliWrap;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class LibreOfficeConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly ILogger<LibreOfficeConverter> _logger;

    private static readonly HashSet<string> DocumentExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "docx", "doc", "docm", "odt", "rtf", "txt", "html", "xhtml", "epub", "fb2", "wpd", "pages", "pdf"
    };

    private static readonly HashSet<string> SpreadsheetExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "xlsx", "xls", "xlsm", "xlsb", "ods", "csv", "tsv", "numbers", "pdf", "html"
    };

    private static readonly HashSet<string> PresentationExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "pptx", "ppt", "pptm", "odp", "key", "pdf", "html", "png", "jpg"
    };

    public LibreOfficeConverter(IBinaryLocator binaryLocator, ILogger<LibreOfficeConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    public string Name => "LibreOffice Headless";
    public int Priority => 95;

    public bool IsAvailable() => _binaryLocator.IsBinaryAvailable("soffice");

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        if (DocumentExts.Contains(src) && (tgt is "pdf" or "docx" or "odt" or "rtf" or "txt" or "html" or "epub"))
        {
            return true;
        }

        if (SpreadsheetExts.Contains(src) && (tgt is "pdf" or "xlsx" or "ods" or "csv" or "html"))
        {
            return true;
        }

        if (PresentationExts.Contains(src) && (tgt is "pdf" or "pptx" or "odp" or "html" or "png" or "jpg"))
        {
            return true;
        }

        return false;
    }

    public async Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        string outputDir, 
        IProgress<int>? progress = null, 
        CancellationToken ct = default)
    {
        var sofficeExe = _binaryLocator.GetBinaryPathOrThrow("soffice");
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var expectedOutput = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");

        Directory.CreateDirectory(outputDir);
        progress?.Report(20);

        _logger.LogInformation("Running LibreOffice headless conversion for {File} -> {Target}", sourcePath, cleanTarget);

        var tempProfile = Path.Combine(Path.GetTempPath(), "soffice_p_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempProfile);
        var profileUri = "file:///" + tempProfile.Replace('\\', '/');

        try
        {
            var args = $"-env:UserInstallation=\"{profileUri}\" --headless --convert-to {cleanTarget} --outdir \"{outputDir}\" \"{sourcePath}\"";

            var result = await CliWrap.Cli.Wrap(sofficeExe)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .ExecuteAsync(ct);

            progress?.Report(100);
        }
        finally
        {
            try { Directory.Delete(tempProfile, true); } catch { }
        }

        if (!File.Exists(expectedOutput))
        {
            // Check for case-insensitive matches in outputDir
            var matches = Directory.GetFiles(outputDir, $"{baseName}.*")
                .Where(f => Path.GetExtension(f).TrimStart('.').Equals(cleanTarget, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 0)
            {
                return matches[0];
            }

            throw new FileNotFoundException($"LibreOffice finished but expected output file '{expectedOutput}' was not found.");
        }

        return expectedOutput;
    }
}
