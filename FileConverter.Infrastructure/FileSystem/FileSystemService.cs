using System.Diagnostics;
using FileConverter.Core.Interfaces;
using FileConverter.Core.Models;

namespace FileConverter.Infrastructure.FileSystem;

public class FileSystemService : IFileSystemService
{
    public FileItemDto GetFileItem(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var ext = fileInfo.Extension.ToLowerInvariant();
        return new FileItemDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = fileInfo.Name,
            FullPath = fileInfo.FullName,
            Extension = ext,
            SizeBytes = fileInfo.Length,
            FormattedSize = FormatBytes(fileInfo.Length),
            Category = CategorizeExtension(ext)
        };
    }

    public List<FileItemDto> InspectFiles(IEnumerable<string> filePaths)
    {
        var results = new List<FileItemDto>();
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    results.Add(GetFileItem(path));
                }
            }
            catch
            {
                // Skip inaccessible files safely
            }
        }
        return results;
    }

    public string GetDefaultOutputDirectory()
    {
        var downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultFolder = Path.Combine(downloads, "Downloads", "ConvertedFiles");
        EnsureDirectoryExists(defaultFolder);
        return defaultFolder;
    }

    public string EnsureDirectoryExists(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(dirPath))
        {
            dirPath = GetDefaultOutputDirectory();
        }

        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        return dirPath;
    }

    public void OpenDirectoryInExplorer(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dirPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    public void OpenFileInDefaultApp(string filePath)
    {
        if (File.Exists(filePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }

    public string GetUniqueDestinationFilePath(string outputDir, string baseName, string targetExtension)
    {
        EnsureDirectoryExists(outputDir);
        var cleanBase = Path.GetFileNameWithoutExtension(baseName);
        var cleanExt = targetExtension.StartsWith('.') ? targetExtension : "." + targetExtension;
        
        var destination = Path.Combine(outputDir, $"{cleanBase}{cleanExt}");
        int counter = 1;
        while (File.Exists(destination))
        {
            destination = Path.Combine(outputDir, $"{cleanBase} ({counter}){cleanExt}");
            counter++;
        }
        return destination;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private static string CategorizeExtension(string ext)
    {
        return ext switch
        {
            ".pdf" => "pdf",
            ".docx" or ".doc" or ".odt" or ".rtf" => "word",
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif" or ".tiff" => "image",
            ".txt" or ".csv" or ".md" or ".json" or ".log" or ".xml" => "text",
            _ => "other"
        };
    }
}
