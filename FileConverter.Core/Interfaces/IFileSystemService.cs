using FileConverter.Core.Models;

namespace FileConverter.Core.Interfaces;

public interface IFileSystemService
{
    FileItemDto GetFileItem(string filePath);
    List<FileItemDto> InspectFiles(IEnumerable<string> filePaths);
    string GetDefaultOutputDirectory();
    string EnsureDirectoryExists(string dirPath);
    void OpenDirectoryInExplorer(string dirPath);
    void OpenFileInDefaultApp(string filePath);
    string GetUniqueDestinationFilePath(string outputDir, string baseName, string targetExtension);
}
