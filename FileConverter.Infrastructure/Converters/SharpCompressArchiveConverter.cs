using System.IO.Compression;
using CliWrap;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace FileConverter.Infrastructure.Converters;

public class SharpCompressArchiveConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly ILogger<SharpCompressArchiveConverter> _logger;

    private static readonly HashSet<string> ArchivableTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "zip", "7z", "tar", "tar.gz", "tgz", "tar.bz2", "xz", "wim"
    };

    private static readonly HashSet<string> ArchivableSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "zip", "7z", "tar", "tar.gz", "tgz", "tar.bz2", "xz", "wim", "rar", "iso", "cab", "gz"
    };

    public SharpCompressArchiveConverter(IBinaryLocator binaryLocator, ILogger<SharpCompressArchiveConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    public string Name => "7-Zip & SharpCompress Archive Engine";
    public int Priority => 85;

    public bool IsAvailable() => true; // SharpCompress is always available in-process

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        // If target is 7z, xz, or wim, requires 7-Zip CLI for writing
        if ((tgt is "7z" or "xz" or "wim") && !_binaryLocator.IsBinaryAvailable("7z"))
        {
            return false;
        }

        return ArchivableSources.Contains(src) && ArchivableTargets.Contains(tgt);
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
        // Handle double extensions like .tar.gz
        if (baseName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
        {
            baseName = Path.GetFileNameWithoutExtension(baseName);
        }

        var outputPath = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");
        Directory.CreateDirectory(outputDir);
        progress?.Report(10);

        var tempExtractDir = Path.Combine(Path.GetTempPath(), "FileConverter_Extract_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempExtractDir);

        try
        {
            // 1. Extract source archive
            if (_binaryLocator.IsBinaryAvailable("7z"))
            {
                var sevenZip = _binaryLocator.GetBinaryPathOrThrow("7z");
                _logger.LogInformation("Extracting archive with 7-Zip CLI: {Source}", sourcePath);
                await CliWrap.Cli.Wrap(sevenZip)
                    .WithArguments($"x \"{sourcePath}\" -o\"{tempExtractDir}\" -y")
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .ExecuteAsync(ct);
            }
            else
            {
                _logger.LogInformation("Extracting archive with SharpCompress in-process engine: {Source}", sourcePath);
                await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        using var archive = ArchiveFactory.OpenArchive(sourcePath);
                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            entry.WriteToDirectory(tempExtractDir, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ArchiveFactory failed, attempting ReaderFactory or Zip fallback on {Source}", sourcePath);
                        try
                        {
                            using var stream = File.OpenRead(sourcePath);
                            using var reader = ReaderFactory.OpenReader(stream);
                            while (reader.MoveToNextEntry())
                            {
                                if (!reader.Entry.IsDirectory)
                                {
                                    reader.WriteEntryToDirectory(tempExtractDir, new ExtractionOptions
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                }
                            }
                        }
                        catch
                        {
                            ZipFile.ExtractToDirectory(sourcePath, tempExtractDir, overwriteFiles: true);
                        }
                    }
                }, ct);
            }

            progress?.Report(50);

            // 2. Repack into target archive
            if (_binaryLocator.IsBinaryAvailable("7z"))
            {
                var sevenZip = _binaryLocator.GetBinaryPathOrThrow("7z");
                _logger.LogInformation("Compressing into {Target} with 7-Zip CLI", outputPath);
                if (File.Exists(outputPath)) File.Delete(outputPath);

                await CliWrap.Cli.Wrap(sevenZip)
                    .WithArguments($"a \"{outputPath}\" \"{tempExtractDir}\\*\" -y")
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .ExecuteAsync(ct);
            }
            else
            {
                _logger.LogInformation("Packaging into {Target} with in-process compressor", outputPath);
                await Task.Run(() =>
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);

                    if (cleanTarget is "tar")
                    {
                        using var outStream = File.Create(outputPath);
                        using var writer = WriterFactory.OpenWriter(outStream, ArchiveType.Tar, new WriterOptions(CompressionType.None));
                        writer.WriteAll(tempExtractDir, "*", SearchOption.AllDirectories);
                    }
                    else if (cleanTarget is "tar.gz" or "tgz")
                    {
                        using var outStream = File.Create(outputPath);
                        using var writer = WriterFactory.OpenWriter(outStream, ArchiveType.Tar, new WriterOptions(CompressionType.GZip));
                        writer.WriteAll(tempExtractDir, "*", SearchOption.AllDirectories);
                    }
                    else
                    {
                        // Default to ZIP
                        ZipFile.CreateFromDirectory(tempExtractDir, outputPath, CompressionLevel.Optimal, false);
                    }
                }, ct);
            }

            progress?.Report(100);

            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException($"Archive conversion did not generate expected output at '{outputPath}'.");
            }

            return outputPath;
        }
        finally
        {
            // Clean up temporary extracted files
            try
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary extraction folder: {Path}", tempExtractDir);
            }
        }
    }
}
