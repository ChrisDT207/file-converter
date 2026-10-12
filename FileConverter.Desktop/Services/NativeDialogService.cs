using System.IO;
using System.Windows;
using FileConverter.Core.Interfaces;
using Microsoft.Win32;

namespace FileConverter.Desktop.Services;

public class NativeDialogService : INativeDialogService
{
    public Task<List<string>> ShowOpenFileDialogAsync(string title, string filter, bool multiSelect = true)
    {
        if (Application.Current == null)
        {
            return Task.FromResult(new List<string>());
        }

        return Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = string.IsNullOrWhiteSpace(filter)
                    ? "Supported Documents (*.pdf;*.docx;*.png;*.jpg;*.txt)|*.pdf;*.docx;*.doc;*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.txt;*.csv;*.md;*.json|All Files (*.*)|*.*"
                    : filter,
                Multiselect = multiSelect,
                CheckFileExists = true
            };

            var owner = Application.Current.MainWindow;
            bool? result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            if (result == true)
            {
                return Task.FromResult(dialog.FileNames.ToList());
            }

            return Task.FromResult(new List<string>());
        });
    }

    public Task<string?> ShowFolderBrowserDialogAsync(string title, string? initialPath = null)
    {
        if (Application.Current == null)
        {
            return Task.FromResult<string?>(null);
        }

        return Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = !string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath) 
                    ? initialPath 
                    : string.Empty
            };

            var owner = Application.Current.MainWindow;
            bool? result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            if (result == true)
            {
                return Task.FromResult<string?>(dialog.FolderName);
            }

            return Task.FromResult<string?>(null);
        });
    }
}
