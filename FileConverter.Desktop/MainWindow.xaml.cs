using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using FileConverter.Core.Interfaces;
using FileConverter.Desktop.Bridge;
using Microsoft.Web.WebView2.Core;

namespace FileConverter.Desktop;

public partial class MainWindow : Window
{
    private readonly IpcBridge _ipcBridge;
    private readonly IFileSystemService _fileSystemService;

    public MainWindow(IpcBridge ipcBridge, IFileSystemService fileSystemService)
    {
        InitializeComponent();
        _ipcBridge = ipcBridge;
        _fileSystemService = fileSystemService;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Initialize CoreWebView2 Environment
            var userDataFolder = Path.Combine(Path.GetTempPath(), "FileConverter_WebView2_UserData");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await MainWebView.EnsureCoreWebView2Async(env);

            // Configure WebView2 settings
            MainWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            MainWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            MainWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Attach IPC Bridge
            _ipcBridge.Attach(MainWebView);

            // Determine UI Source: Development Vite server vs Production Built Assets
            var devServerUrl = "http://localhost:5173";
            bool isDevServerRunning = await CheckDevServerRunningAsync(devServerUrl);

            if (isDevServerRunning)
            {
                MainWebView.CoreWebView2.Navigate(devServerUrl);
            }
            else
            {
                // Production: Virtual host name mapping to local React dist/wwwroot
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var distPath = Path.Combine(baseDir, "wwwroot");

                if (!Directory.Exists(distPath))
                {
                    var releaseWwwroot = Path.Combine(baseDir, "ReleaseBuild", "wwwroot");
                    if (Directory.Exists(releaseWwwroot))
                    {
                        distPath = releaseWwwroot;
                    }
                }

                if (!Directory.Exists(distPath))
                {
                    var distWwwroot = Path.Combine(baseDir, "dist", "wwwroot");
                    if (Directory.Exists(distWwwroot))
                    {
                        distPath = distWwwroot;
                    }
                }

                if (!Directory.Exists(distPath))
                {
                    var clientDist = Path.Combine(baseDir, "client", "dist");
                    if (Directory.Exists(clientDist))
                    {
                        distPath = clientDist;
                    }
                }

                if (!Directory.Exists(distPath))
                {
                    // Fallback to relative client/dist for direct dev test
                    var fallbackPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "client", "dist"));
                    if (Directory.Exists(fallbackPath))
                    {
                        distPath = fallbackPath;
                    }
                }

                if (Directory.Exists(distPath))
                {
                    MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "app.assets",
                        distPath,
                        CoreWebView2HostResourceAccessKind.Allow
                    );
                    MainWebView.CoreWebView2.Navigate("https://app.assets/index.html");
                }
                else
                {
                    // If no assets yet, display instruction page
                    MainWebView.NavigateToString(@"
                        <html>
                        <body style='font-family:sans-serif;padding:40px;background:#0f172a;color:#fff;'>
                            <h2>File converter Backend Running</h2>
                            <p>React frontend build not found at wwwroot. Please build the frontend:</p>
                            <pre style='background:#1e293b;padding:15px;border-radius:8px;'>cd client &amp;&amp; npm run build</pre>
                            <p>Or start Vite dev server: <code>npm run dev</code> and restart the app.</p>
                        </body>
                        </html>
                    ");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing desktop application: {ex.Message}", "File converter Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task<bool> CheckDevServerRunningAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(400) };
            var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                var inspected = _fileSystemService.InspectFiles(files);
                _ipcBridge.SendEvent("filesDropped", inspected);
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var dropList = Clipboard.GetFileDropList();
                    var fileList = new List<string>();
                    foreach (string? f in dropList)
                    {
                        if (!string.IsNullOrWhiteSpace(f) && (File.Exists(f) || Directory.Exists(f)))
                        {
                            fileList.Add(f);
                        }
                    }

                    if (fileList.Count > 0)
                    {
                        var inspected = _fileSystemService.InspectFiles(fileList);
                        _ipcBridge.SendEvent("filesDropped", inspected);
                    }
                }
            }
            catch
            {
                // Ignore clipboard exceptions
            }
        }
    }
}