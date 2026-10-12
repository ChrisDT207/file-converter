using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class ManagedFallbackConverter : IFileConverter
{
    private readonly IDocumentConversionService _documentService;
    private readonly IDataConversionService _dataService;
    private readonly IImageConversionService _imageService;
    private readonly ILogger<ManagedFallbackConverter> _logger;

    public ManagedFallbackConverter(
        IDocumentConversionService documentService,
        IDataConversionService dataService,
        IImageConversionService imageService,
        ILogger<ManagedFallbackConverter> logger)
    {
        _documentService = documentService;
        _dataService = dataService;
        _imageService = imageService;
        _logger = logger;
    }

    public string Name => "Pure Managed C# Engine";
    public int Priority => 10; // Base fallback

    public bool IsAvailable() => true;

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        // Data / Spreadsheets
        if (src is "csv" or "tsv" or "json" or "xml" or "yaml" or "yml" or "xlsx" &&
            tgt is "csv" or "json" or "xml" or "yaml" or "html" or "txt")
        {
            return true;
        }

        // Documents
        if (src is "pdf" or "docx" or "doc" or "md" or "txt" or "html" &&
            tgt is "pdf" or "docx" or "txt" or "md" or "html" or "png" or "jpg")
        {
            return true;
        }

        // Images
        if (src is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "ico" or "tiff" &&
            tgt is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "ico" or "tiff" or "pdf")
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
        var srcExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        var tgtExt = targetExtension.TrimStart('.').ToLowerInvariant();

        progress?.Report(20);

        if (srcExt is "csv" or "tsv" or "json" or "xml" or "yaml" or "yml" or "xlsx")
        {
            var res = await _dataService.ConvertDataAsync(sourcePath, tgtExt, outputDir, ct);
            progress?.Report(100);
            return res;
        }

        if (srcExt is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "ico" or "tiff")
        {
            var res = await _imageService.ConvertImageAsync(sourcePath, tgtExt, outputDir, ct);
            progress?.Report(100);
            return res;
        }

        // Document fallback
        var docRes = await _documentService.ConvertDocumentAsync(sourcePath, tgtExt, outputDir, ct);
        progress?.Report(100);
        return docRes;
    }
}
