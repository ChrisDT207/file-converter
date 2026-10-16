using System.IO;
using System.Text.Json;
using System.Windows;
using FileConverter.Core.Interfaces;
using FileConverter.Core.Models;
using Microsoft.Web.WebView2.Wpf;

namespace FileConverter.Desktop.Bridge;

public class IpcBridge
{
    private readonly IConversionOrchestrator _orchestrator;
    private readonly IFileSystemService _fileSystemService;
    private readonly INativeDialogService _dialogService;
    private readonly IBinaryLocator _binaryLocator;
    private readonly JsonSerializerOptions _jsonOptions;
    private WebView2? _webView;

    public event Action<bool>? ThemeChanged;

    public IpcBridge(
        IConversionOrchestrator orchestrator,
        IFileSystemService fileSystemService,
        INativeDialogService dialogService,
        IBinaryLocator binaryLocator)
    {
        _orchestrator = orchestrator;
        _fileSystemService = fileSystemService;
        _dialogService = dialogService;
        _binaryLocator = binaryLocator;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void Attach(WebView2 webView)
    {
        _webView = webView;
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    private async void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        var rawJson = e.TryGetWebMessageAsString();
        if (string.IsNullOrWhiteSpace(rawJson)) return;

        IpcRequestMessage? request = null;
        try
        {
            request = JsonSerializer.Deserialize<IpcRequestMessage>(rawJson, _jsonOptions);
        }
        catch
        {
            return;
        }

        if (request == null) return;

        try
        {
            var responseData = await HandleActionAsync(request.Action, request.Payload, request.Id);
            SendSuccess(request.Id, request.Action, responseData);
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Action, ex.Message);
        }
    }

    private async Task<object?> HandleActionAsync(string action, JsonElement? payload, string requestId)
    {
        switch (action.ToLowerInvariant())
        {
            case "getinitialconfig":
            {
                var defaultOut = _fileSystemService.GetDefaultOutputDirectory();
                return new
                {
                    DefaultOutputDirectory = defaultOut,
                    AppVersion = "2.0.0",
                    Platform = "Windows Desktop",
                    Tools = _binaryLocator.GetToolsStatus()
                };
            }

            case "getconversionmatrix":
            {
                return ConversionRegistry.GetAllMatrix(_binaryLocator);
            }

            case "gettargetformats":
            {
                string ext = string.Empty;
                if (payload.HasValue)
                {
                    if (payload.Value.ValueKind == JsonValueKind.String)
                    {
                        ext = payload.Value.GetString() ?? string.Empty;
                    }
                    else if (payload.Value.TryGetProperty("extension", out var prop))
                    {
                        ext = prop.GetString() ?? string.Empty;
                    }
                }
                return ConversionRegistry.GetAvailableTargetsForExtension(ext, _binaryLocator);
            }

            case "getmimeinfo":
            {
                string ext = string.Empty;
                if (payload.HasValue)
                {
                    if (payload.Value.ValueKind == JsonValueKind.String)
                    {
                        ext = payload.Value.GetString() ?? string.Empty;
                    }
                    else if (payload.Value.TryGetProperty("extension", out var prop))
                    {
                        ext = prop.GetString() ?? string.Empty;
                    }
                }
                var clean = ext.TrimStart('.').ToLowerInvariant();
                if (ConversionRegistry.Formats.TryGetValue(clean, out var def))
                {
                    return def;
                }
                return new FormatDefinition
                {
                    Extension = clean,
                    Label = clean.ToUpperInvariant(),
                    Category = "document",
                    MimeType = "application/octet-stream"
                };
            }

            case "gettoolsstatus":
            {
                return _binaryLocator.GetToolsStatus();
            }

            case "selectfiles":
            {
                var files = await _dialogService.ShowOpenFileDialogAsync(
                    "Select Files to Convert", 
                    "All Supported Formats (*.*)|*.*|Documents (*.docx;*.doc;*.odt;*.rtf;*.txt;*.md;*.html;*.epub;*.pdf)|*.docx;*.doc;*.docm;*.odt;*.rtf;*.txt;*.md;*.html;*.xhtml;*.epub;*.mobi;*.azw3;*.fb2;*.latex;*.tex;*.rst;*.org;*.wpd;*.pages;*.pdf|Spreadsheets & Data (*.xlsx;*.xls;*.ods;*.csv;*.tsv;*.json;*.xml;*.yaml)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.ods;*.csv;*.tsv;*.json;*.xml;*.yaml;*.yml;*.numbers|Presentations (*.pptx;*.ppt;*.odp;*.key)|*.pptx;*.ppt;*.pptm;*.odp;*.key|Images & RAW (*.jpg;*.png;*.webp;*.avif;*.gif;*.tiff;*.bmp;*.ico;*.heic;*.psd;*.svg;*.cr2;*.nef;*.arw;*.dng)|*.jpg;*.jpeg;*.png;*.webp;*.avif;*.gif;*.tiff;*.tif;*.bmp;*.ico;*.heic;*.heif;*.psd;*.svg;*.eps;*.ai;*.tga;*.hdr;*.exr;*.dds;*.pcx;*.pnm;*.xpm;*.j2k;*.jp2;*.cr2;*.cr3;*.nef;*.arw;*.dng;*.raf;*.orf;*.rw2|Audio (*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a;*.opus)|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.oga;*.m4a;*.wma;*.alac;*.aiff;*.opus;*.amr;*.au;*.voc|Video (*.mp4;*.mkv;*.mov;*.avi;*.webm)|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.flv;*.wmv;*.m4v;*.3gp;*.ts;*.m2ts;*.vob;*.ogv;*.asf;*.rmvb|3D Models (*.obj;*.stl;*.gltf;*.glb;*.ply;*.fbx;*.blend)|*.obj;*.stl;*.gltf;*.glb;*.ply;*.fbx;*.dae;*.3ds;*.blend|Archives (*.zip;*.7z;*.tar;*.tar.gz;*.rar;*.iso)|*.zip;*.7z;*.tar;*.tar.gz;*.tgz;*.tar.bz2;*.xz;*.wim;*.rar;*.iso;*.cab;*.gz",
                    multiSelect: true
                );

                return _fileSystemService.InspectFiles(files);
            }

            case "getclipboardfiles":
            {
                var files = new List<string>();
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (Clipboard.ContainsFileDropList())
                        {
                            var dropList = Clipboard.GetFileDropList();
                            foreach (string? file in dropList)
                            {
                                if (!string.IsNullOrWhiteSpace(file) && (File.Exists(file) || Directory.Exists(file)))
                                {
                                    files.Add(file);
                                }
                            }
                        }
                    });
                }
                catch
                {
                    // Ignore clipboard access exceptions
                }

                return _fileSystemService.InspectFiles(files);
            }

            case "inspectpaths":
            {
                if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Array)
                {
                    var paths = JsonSerializer.Deserialize<List<string>>(payload.Value.GetRawText(), _jsonOptions) ?? new();
                    return _fileSystemService.InspectFiles(paths);
                }
                return new List<FileItemDto>();
            }

            case "selectfolder":
            {
                string? initial = null;
                if (payload.HasValue && payload.Value.TryGetProperty("initialPath", out var prop))
                {
                    initial = prop.GetString();
                }

                var selected = await _dialogService.ShowFolderBrowserDialogAsync("Select Destination Folder", initial);
                return new { SelectedFolder = selected };
            }

            case "convert":
            {
                if (!payload.HasValue) throw new ArgumentException("Conversion payload is missing.");

                var convRequest = JsonSerializer.Deserialize<ConversionRequestDto>(payload.Value.GetRawText(), _jsonOptions);
                if (convRequest == null) throw new ArgumentException("Invalid conversion request format.");

                var progress = new Progress<ProgressReportDto>(report =>
                {
                    SendEvent("conversionProgress", report);
                });

                return await _orchestrator.ExecuteConversionAsync(convRequest, progress);
            }

            case "opendirectory":
            {
                if (payload.HasValue && payload.Value.TryGetProperty("path", out var p))
                {
                    var dir = p.GetString();
                    if (!string.IsNullOrEmpty(dir))
                    {
                        _fileSystemService.OpenDirectoryInExplorer(dir);
                        return true;
                    }
                }
                return false;
            }

            case "openfile":
            {
                if (payload.HasValue && payload.Value.TryGetProperty("path", out var p))
                {
                    var file = p.GetString();
                    if (!string.IsNullOrEmpty(file))
                    {
                        _fileSystemService.OpenFileInDefaultApp(file);
                        return true;
                    }
                }
                return false;
            }

            case "settheme":
            {
                string theme = "dark";
                if (payload.HasValue)
                {
                    if (payload.Value.ValueKind == JsonValueKind.String)
                    {
                        theme = payload.Value.GetString() ?? "dark";
                    }
                    else if (payload.Value.TryGetProperty("theme", out var prop))
                    {
                        theme = prop.GetString() ?? "dark";
                    }
                }
                bool isDark = theme.Equals("dark", StringComparison.OrdinalIgnoreCase);
                ThemeChanged?.Invoke(isDark);
                return true;
            }

            default:
                throw new NotSupportedException($"Bridge action '{action}' is not supported.");
        }
    }

    private void SendSuccess(string id, string action, object? data)
    {
        if (_webView?.CoreWebView2 == null) return;

        var response = new IpcResponseMessage
        {
            Id = id,
            Action = action,
            Success = true,
            Data = data,
            Error = null
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        _webView.Dispatcher.Invoke(() =>
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        });
    }

    private void SendError(string id, string action, string error)
    {
        if (_webView?.CoreWebView2 == null) return;

        var response = new IpcResponseMessage
        {
            Id = id,
            Action = action,
            Success = false,
            Data = null,
            Error = error
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        _webView.Dispatcher.Invoke(() =>
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        });
    }

    public void SendEvent(string eventName, object? data)
    {
        if (_webView?.CoreWebView2 == null) return;

        var response = new IpcResponseMessage
        {
            Id = "EVENT_" + Guid.NewGuid().ToString("N"),
            Action = eventName,
            Success = true,
            Data = data,
            Error = null
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        _webView.Dispatcher.Invoke(() =>
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        });
    }
}
