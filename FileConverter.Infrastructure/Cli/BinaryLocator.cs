using System.Diagnostics;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Cli;

public class BinaryLocator : IBinaryLocator
{
    private readonly ILogger<BinaryLocator> _logger;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public BinaryLocator(ILogger<BinaryLocator> logger)
    {
        _logger = logger;
    }

    public string? FindBinary(string binaryName)
    {
        var cleanName = Path.GetFileNameWithoutExtension(binaryName).ToLowerInvariant();

        if (_cache.TryGetValue(cleanName, out var cachedPath))
        {
            return cachedPath;
        }

        var resolvedPath = ResolveBinaryPath(cleanName);
        _cache[cleanName] = resolvedPath;

        if (resolvedPath != null)
        {
            _logger.LogInformation("Discovered CLI binary '{Binary}': {Path}", cleanName, resolvedPath);
        }
        else
        {
            _logger.LogDebug("Binary '{Binary}' not found on system PATH or default directories.", cleanName);
        }

        return resolvedPath;
    }

    public bool IsBinaryAvailable(string binaryName)
    {
        return FindBinary(binaryName) != null;
    }

    public string GetBinaryPathOrThrow(string binaryName)
    {
        var path = FindBinary(binaryName);
        if (string.IsNullOrEmpty(path))
        {
            throw new FileNotFoundException($"Required binary '{binaryName}' is not installed or could not be found on the system.");
        }
        return path;
    }

    public Dictionary<string, bool> GetToolsStatus()
    {
        var tools = new[] { "soffice", "pandoc", "magick", "ffmpeg", "7z", "blender" };
        var status = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
        {
            status[tool] = IsBinaryAvailable(tool);
        }
        return status;
    }

    private string? ResolveBinaryPath(string name)
    {
        // 1. Check App local tools directory first
        var appBase = AppDomain.CurrentDomain.BaseDirectory;
        var localCandidates = new[]
        {
            Path.Combine(appBase, "tools", $"{name}.exe"),
            Path.Combine(appBase, "tools", name, $"{name}.exe"),
            Path.Combine(appBase, $"{name}.exe")
        };

        foreach (var c in localCandidates)
        {
            if (File.Exists(c)) return Path.GetFullPath(c);
        }

        // 2. Check PATH environment variable
        var pathFromEnv = FindInPath(name);
        if (pathFromEnv != null) return pathFromEnv;

        // 3. Search standard Windows installation locations based on tool
        switch (name)
        {
            case "soffice" or "libreoffice":
                return ProbePaths(
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
                );

            case "pandoc":
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return ProbePaths(
                    Path.Combine(localAppData, "Pandoc", "pandoc.exe"),
                    @"C:\Program Files\Pandoc\pandoc.exe",
                    @"C:\Program Files (x86)\Pandoc\pandoc.exe"
                );

            case "magick":
                var imPath = ProbeWildcard(@"C:\Program Files", "ImageMagick*", "magick.exe")
                             ?? ProbeWildcard(@"C:\Program Files (x86)", "ImageMagick*", "magick.exe");
                return imPath ?? ProbePaths(
                    @"C:\Program Files\ImageMagick\magick.exe",
                    @"C:\Program Files (x86)\ImageMagick\magick.exe"
                );

            case "ffmpeg":
                var ffmpegLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ffmpeg", "bin", "ffmpeg.exe");
                return ProbePaths(
                    @"C:\ffmpeg\bin\ffmpeg.exe",
                    @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                    @"C:\tools\ffmpeg\bin\ffmpeg.exe",
                    ffmpegLocal
                ) ?? ProbeWildcard(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages"), "*ffmpeg*", "ffmpeg.exe");

            case "7z" or "7za":
                return ProbePaths(
                    @"C:\Program Files\7-Zip\7z.exe",
                    @"C:\Program Files (x86)\7-Zip\7z.exe"
                );

            case "blender":
                return ProbeWildcard(@"C:\Program Files\Blender Foundation", "Blender*", "blender.exe");

            default:
                return null;
        }
    }

    private static string? FindInPath(string binaryName)
    {
        var exeName = binaryName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? binaryName : $"{binaryName}.exe";
        var envPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(envPath)) return null;

        var paths = envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            try
            {
                var full = Path.Combine(path.Trim('\"'), exeName);
                if (File.Exists(full)) return Path.GetFullPath(full);
            }
            catch
            {
                // Ignore invalid paths in PATH
            }
        }

        return null;
    }

    private static string? ProbePaths(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        return null;
    }

    private static string? ProbeWildcard(string baseDir, string dirPattern, string targetExe)
    {
        try
        {
            if (!Directory.Exists(baseDir)) return null;
            var dirs = Directory.GetDirectories(baseDir, dirPattern);
            foreach (var dir in dirs)
            {
                var subFile = Path.Combine(dir, targetExe);
                if (File.Exists(subFile)) return Path.GetFullPath(subFile);

                var binSubFile = Path.Combine(dir, "bin", targetExe);
                if (File.Exists(binSubFile)) return Path.GetFullPath(binSubFile);
            }
        }
        catch
        {
            // Ignore security/IO exceptions on directories
        }
        return null;
    }
}
