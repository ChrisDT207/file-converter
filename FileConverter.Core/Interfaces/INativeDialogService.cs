namespace FileConverter.Core.Interfaces;

public interface INativeDialogService
{
    Task<List<string>> ShowOpenFileDialogAsync(string title, string filter, bool multiSelect = true);
    Task<string?> ShowFolderBrowserDialogAsync(string title, string? initialPath = null);
}
