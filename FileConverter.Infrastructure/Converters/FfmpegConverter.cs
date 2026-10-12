using CliWrap;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class FfmpegConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly ILogger<FfmpegConverter> _logger;

    private static readonly HashSet<string> AudioExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "oga", "m4a", "wma", "alac", "aiff", "opus", "amr", "au", "voc"
    };

    private static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "mov", "avi", "webm", "flv", "wmv", "m4v", "3gp", "ts", "m2ts", "vob", "ogv", "asf", "rmvb"
    };

    private static readonly HashSet<string> AnimationExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "gif", "webp"
    };

    public FfmpegConverter(IBinaryLocator binaryLocator, ILogger<FfmpegConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    public string Name => "FFmpeg Media Converter";
    public int Priority => 100;

    public bool IsAvailable() => _binaryLocator.IsBinaryAvailable("ffmpeg");

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        // 1. Audio to Audio
        if (AudioExts.Contains(src) && AudioExts.Contains(tgt)) return true;

        // 2. Video to Video
        if (VideoExts.Contains(src) && VideoExts.Contains(tgt)) return true;

        // 3. Video to Audio (extraction)
        if (VideoExts.Contains(src) && AudioExts.Contains(tgt)) return true;

        // 4. Video to Animation (gif / webp)
        if (VideoExts.Contains(src) && AnimationExts.Contains(tgt)) return true;

        return false;
    }

    public async Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        string outputDir, 
        IProgress<int>? progress = null, 
        CancellationToken ct = default)
    {
        var ffmpegExe = _binaryLocator.GetBinaryPathOrThrow("ffmpeg");
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");

        // Avoid overwriting identical source and destination
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Path.Combine(outputDir, $"{baseName}_converted.{cleanTarget}");
        }

        Directory.CreateDirectory(outputDir);
        progress?.Report(10);

        var srcExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        var arguments = BuildArguments(sourcePath, outputPath, srcExt, cleanTarget);

        _logger.LogInformation("Executing FFmpeg conversion: {Exe} {Args}", ffmpegExe, arguments);

        var result = await CliWrap.Cli.Wrap(ffmpegExe)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync(ct);

        progress?.Report(100);

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException($"FFmpeg did not produce output file at '{outputPath}'.");
        }

        return outputPath;
    }

    private static string BuildArguments(string input, string output, string srcExt, string tgtExt)
    {
        // Video to Animated GIF
        if (VideoExts.Contains(srcExt) && tgtExt == "gif")
        {
            return $"-y -i \"{input}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{output}\"";
        }

        // Video to Animated WebP
        if (VideoExts.Contains(srcExt) && tgtExt == "webp")
        {
            return $"-y -i \"{input}\" -vcodec libwebp -filter:v fps=fps=20 -lossless 0 -compression_level 4 -q:v 70 -loop 0 \"{output}\"";
        }

        // Video to Audio extraction
        if (VideoExts.Contains(srcExt) && AudioExts.Contains(tgtExt))
        {
            var codec = tgtExt switch
            {
                "mp3" => "-vn -acodec libmp3lame -q:a 2",
                "wav" => "-vn -acodec pcm_s16le",
                "flac" => "-vn -acodec flac",
                "aac" => "-vn -acodec aac -b:a 192k",
                "ogg" or "oga" => "-vn -acodec libvorbis -q:a 4",
                "opus" => "-vn -acodec libopus -b:a 128k",
                "m4a" => "-vn -acodec aac -b:a 192k",
                _ => "-vn"
            };
            return $"-y -i \"{input}\" {codec} \"{output}\"";
        }

        // Audio to Audio
        if (AudioExts.Contains(srcExt) && AudioExts.Contains(tgtExt))
        {
            var codec = tgtExt switch
            {
                "mp3" => "-acodec libmp3lame -q:a 2",
                "wav" => "-acodec pcm_s16le",
                "flac" => "-acodec flac",
                "aac" => "-acodec aac -b:a 192k",
                "ogg" or "oga" => "-acodec libvorbis -q:a 4",
                "opus" => "-acodec libopus -b:a 128k",
                _ => string.Empty
            };
            return $"-y -i \"{input}\" {codec} \"{output}\"".Trim();
        }

        // Video to Video
        return $"-y -i \"{input}\" -c:v libx264 -c:a aac -movflags +faststart \"{output}\"";
    }
}
