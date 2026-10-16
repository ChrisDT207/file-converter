using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class ManagedFallbackConverter : IFileConverter
{
    private readonly IDocumentConversionService _documentService;
    private readonly IDataConversionService _dataService;
    private readonly IImageConversionService _imageService;
    private readonly IPresentationConversionService _presentationService;
    private readonly ILogger<ManagedFallbackConverter> _logger;

    public ManagedFallbackConverter(
        IDocumentConversionService documentService,
        IDataConversionService dataService,
        IImageConversionService imageService,
        IPresentationConversionService presentationService,
        ILogger<ManagedFallbackConverter> logger)
    {
        _documentService = documentService;
        _dataService = dataService;
        _imageService = imageService;
        _presentationService = presentationService;
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

        // Presentations (PPTX, PPT -> PDF, TXT, MD, HTML, PNG, JPG)
        if (src is "pptx" or "ppt" &&
            tgt is "pdf" or "txt" or "md" or "html" or "png" or "jpg" or "jpeg")
        {
            return true;
        }

        // Data / Spreadsheets (CSV, TSV, JSON, XML, YAML, XLSX, XLS -> CSV, TSV, JSON, XML, YAML, HTML, TXT, XLSX, PDF)
        if (src is "csv" or "tsv" or "json" or "xml" or "yaml" or "yml" or "xlsx" or "xls" &&
            tgt is "csv" or "tsv" or "json" or "xml" or "yaml" or "yml" or "html" or "txt" or "xlsx" or "pdf")
        {
            return true;
        }

        // Documents (PDF, DOCX, DOC, MD, TXT, LOG, HTML, HTM -> PDF, DOCX, TXT, MD, HTML, PNG, JPG, BMP, RTF)
        if (src is "pdf" or "docx" or "doc" or "md" or "txt" or "log" or "html" or "htm" &&
            tgt is "pdf" or "docx" or "txt" or "md" or "html" or "png" or "jpg" or "jpeg" or "bmp" or "rtf")
        {
            return true;
        }

        // Images (PNG, JPG, JPEG, BMP, GIF, ICO, TIFF, TIF, WEBP -> PNG, JPG, JPEG, BMP, GIF, ICO, TIFF, TIF, WEBP, PDF)
        if (src is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "ico" or "tiff" or "tif" or "webp" &&
            tgt is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "ico" or "tiff" or "tif" or "webp" or "pdf")
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

        if (srcExt is "pptx" or "ppt")
        {
            var res = await _presentationService.ConvertPresentationAsync(sourcePath, tgtExt, outputDir, ct);
            progress?.Report(100);
            return res;
        }

        if (srcExt is "csv" or "tsv" or "json" or "xml" or "yaml" or "yml" or "xlsx" or "xls")
        {
            var res = await _dataService.ConvertDataAsync(sourcePath, tgtExt, outputDir, ct);
            progress?.Report(100);
            return res;
        }

        if (srcExt is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "ico" or "tiff" or "tif" or "webp")
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
