using System.Diagnostics;
using CliWrap;
using FileConverter.Core.Interfaces;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class ImageMagickConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly ILogger<ImageMagickConverter> _logger;

    private static readonly HashSet<string> StandardImageExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "webp", "avif", "gif", "tiff", "tif", "bmp", "ico",
        "heic", "heif", "psd", "svg", "eps", "ai", "tga", "hdr", "exr", "dds",
        "pcx", "pnm", "xpm", "j2k", "jp2", "pdf"
    };

    private static readonly HashSet<string> CameraRawExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cr2", "cr3", "nef", "arw", "dng", "raf", "orf", "rw2"
    };

    public ImageMagickConverter(IBinaryLocator binaryLocator, ILogger<ImageMagickConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    public string Name => "ImageMagick & Magick.NET Studio";
    public int Priority => 95;

    // Magick.NET is embedded in-process so it is always available
    public bool IsAvailable() => true;

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        // Camera RAW is read-only -> converts to standard raster targets
        if (CameraRawExts.Contains(src) && (StandardImageExts.Contains(tgt) || tgt is "jpg" or "png" or "webp" or "tiff" or "bmp" or "pdf"))
        {
            return true;
        }

        // Standard image cross-conversion
        if (StandardImageExts.Contains(src) && StandardImageExts.Contains(tgt))
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
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");

        Directory.CreateDirectory(outputDir);
        progress?.Report(20);

        // Try in-process Magick.NET first
        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var collection = new MagickImageCollection(sourcePath);
                progress?.Report(60);

                if (cleanTarget is "ico")
                {
                    // Ensure icon sizes are compatible
                    foreach (var frame in collection)
                    {
                        if (frame.Width > 256 || frame.Height > 256)
                        {
                            frame.Resize(256, 256);
                        }
                    }
                }

                collection.Write(outputPath);
            }, ct);

            progress?.Report(100);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Magick.NET in-process conversion failed for {File}. Trying ImageMagick CLI if available...", sourcePath);

            // Fallback to ImageMagick CLI if installed
            if (_binaryLocator.IsBinaryAvailable("magick"))
            {
                var magickExe = _binaryLocator.GetBinaryPathOrThrow("magick");
                var args = $"convert \"{sourcePath}\" \"{outputPath}\"";

                await CliWrap.Cli.Wrap(magickExe)
                    .WithArguments(args)
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .ExecuteAsync(ct);

                progress?.Report(100);
                if (File.Exists(outputPath)) return outputPath;
            }

            throw;
        }
    }
}
