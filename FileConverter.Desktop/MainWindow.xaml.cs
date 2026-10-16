using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FileConverter.Core.Interfaces;
using FileConverter.Desktop.Bridge;
using Microsoft.Web.WebView2.Core;

namespace FileConverter.Desktop;

public partial class MainWindow : Window
{
    private readonly IpcBridge _ipcBridge;
    private readonly IFileSystemService _fileSystemService;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    public MainWindow(IpcBridge ipcBridge, IFileSystemService fileSystemService)
    {
        InitializeComponent();
        _ipcBridge = ipcBridge;
        _fileSystemService = fileSystemService;

        _ipcBridge.ThemeChanged += OnThemeChanged;
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Default immediately to dark theme on startup so the window opens with dark titlebar & controls
        ApplyTheme(true);
    }

    private void OnThemeChanged(bool isDark)
    {
        ApplyTheme(isDark);
    }

    public void ApplyTheme(bool isDark)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ApplyTheme(isDark));
            return;
        }

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int useDarkMode = isDark ? 1 : 0;

                // Modern Windows 10 (20H1+) and Windows 11 immersive dark mode
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                if (hr != 0)
                {
                    // Fallback for older Windows 10 builds (1809 - 1909)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                }

                // Windows 11 (build 22000+) custom caption and text colors
                if (Environment.OSVersion.Version.Build >= 22000)
                {
                    // COLORREF format is 0x00BBGGRR
                    // Dark: #0B1120 -> R=0x0B, G=0x11, B=0x20 => 0x0020110B
                    // Light: #F8FAFC -> R=0xF8, G=0xFA, B=0xFC => 0x00FCFAF8
                    int captionColor = isDark ? 0x0020110B : 0x00FCFAF8;
                    DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                    // Text color: White for dark mode, dark slate (#0F172A) for light mode
                    int textColor = isDark ? 0x00FFFFFF : 0x002A170F;
                    DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
                }

                // Force Windows non-client area (title bar and min/max/close buttons) to redraw immediately
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }

            // Match WPF window background and WebView2 default background color
            var bgHex = isDark ? "#0B1120" : "#F8FAFC";
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));

            try
            {
                if (MainWebView != null)
                {
                    MainWebView.DefaultBackgroundColor = isDark
                        ? System.Drawing.Color.FromArgb(255, 11, 17, 32)
                        : System.Drawing.Color.FromArgb(255, 248, 250, 252);
                }
            }
            catch
            {
                // WebView2 COM wrapper may not be ready during early SourceInitialized
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set window theme: {ex.Message}");
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Set dark background on WebView2 prior to navigation to eliminate white flash
            MainWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 11, 17, 32);

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