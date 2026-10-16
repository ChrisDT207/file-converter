using System.Reflection;
using System.Runtime.InteropServices;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class ExcelConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly IFileSystemService _fileSystemService;
    private readonly IDataConversionService? _dataService;
    private readonly ILogger<ExcelConverter> _logger;

    private static readonly HashSet<string> SupportedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "xlsx", "xls", "xlsm", "xlsb", "csv", "tsv", "json", "xml", "yaml", "yml"
    };

    private static readonly HashSet<string> SupportedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "xlsx", "csv", "ods", "html", "txt"
    };

    public ExcelConverter(
        IBinaryLocator binaryLocator,
        IFileSystemService fileSystemService,
        ILogger<ExcelConverter> logger,
        IDataConversionService? dataService = null)
    {
        _binaryLocator = binaryLocator;
        _fileSystemService = fileSystemService;
        _dataService = dataService;
        _logger = logger;
    }

    public string Name => "Microsoft Excel Native Engine";
    public int Priority => 98;

    public bool IsAvailable()
    {
        return _binaryLocator.IsBinaryAvailable("excel");
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        if (!IsAvailable()) return false;

        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        if (src is "json" or "xml" or "yaml" or "yml")
        {
            return tgt == "ods";
        }

        return SupportedSources.Contains(src) && SupportedTargets.Contains(tgt);
    }

    public async Task<string> ConvertAsync(
        string sourcePath,
        string targetExtension,
        string outputDir,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var srcExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var expectedOutput = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");

        _fileSystemService.EnsureDirectoryExists(outputDir);
        progress?.Report(15);

        string fileToOpen = sourcePath;
        string? tempDirToClean = null;

        if (srcExt is "json" or "xml" or "yaml" or "yml" && _dataService != null)
        {
            tempDirToClean = Path.Combine(Path.GetTempPath(), "FC_ExcelTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirToClean);
            fileToOpen = await _dataService.ConvertDataAsync(sourcePath, "xlsx", tempDirToClean, ct);
        }

        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    throw new InvalidOperationException("Microsoft Excel is not installed or Excel.Application COM is unavailable.");
                }

                object? excelApp = null;
                object? workbooks = null;
                object? wb = null;

                try
                {
                    excelApp = Activator.CreateInstance(excelType);
                    if (excelApp == null)
                    {
                        throw new InvalidOperationException("Failed to instantiate Excel.Application COM object.");
                    }

                    // Visible = false, DisplayAlerts = false
                    excelApp.GetType().InvokeMember("Visible", BindingFlags.SetProperty, null, excelApp, new object[] { false });
                    excelApp.GetType().InvokeMember("DisplayAlerts", BindingFlags.SetProperty, null, excelApp, new object[] { false });

                    progress?.Report(35);
                    ct.ThrowIfCancellationRequested();

                    workbooks = excelApp.GetType().InvokeMember("Workbooks", BindingFlags.GetProperty, null, excelApp, null);
                    if (workbooks == null)
                    {
                        throw new InvalidOperationException("Failed to get Workbooks collection from Excel.");
                    }

                    // Open(Filename, ReadOnly: true)
                    var openArgs = new object[] { Path.GetFullPath(fileToOpen), Type.Missing, true };
                    wb = workbooks.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, workbooks, openArgs);
                    if (wb == null)
                    {
                        throw new InvalidOperationException($"Excel failed to open '{fileToOpen}'.");
                    }

                    progress?.Report(65);
                    ct.ThrowIfCancellationRequested();

                    if (File.Exists(expectedOutput)) File.Delete(expectedOutput);

                    if (cleanTarget == "pdf")
                    {
                        // ExportAsFixedFormat(Type: 0 (xlTypePDF), Filename: expectedOutput)
                        var exportArgs = new object[] { 0, Path.GetFullPath(expectedOutput) };
                        wb.GetType().InvokeMember("ExportAsFixedFormat", BindingFlags.InvokeMethod, null, wb, exportArgs);
                    }
                    else
                    {
                        // xlOpenXMLWorkbook = 51, xlCSV = 6, xlOpenDocumentSpreadsheet = 60, xlHtml = 44, xlCurrentPlatformText = -4158
                        int fileFormat = cleanTarget switch
                        {
                            "xlsx" => 51,
                            "csv" => 6,
                            "ods" => 60,
                            "html" => 44,
                            "txt" => -4158,
                            _ => 51
                        };

                        var saveArgs = new object[] { Path.GetFullPath(expectedOutput), fileFormat };
                        wb.GetType().InvokeMember("SaveAs", BindingFlags.InvokeMethod, null, wb, saveArgs);
                    }

                    progress?.Report(95);

                    if (!File.Exists(expectedOutput))
                    {
                        throw new FileNotFoundException($"Expected Excel output file was not created: '{expectedOutput}'", expectedOutput);
                    }

                    return expectedOutput;
                }
                finally
                {
                    if (wb != null)
                    {
                        try
                        {
                            // Close(SaveChanges: false)
                            wb.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, wb, new object[] { false });
                        }
                        catch { }
                        Marshal.ReleaseComObject(wb);
                    }

                    if (workbooks != null)
                    {
                        Marshal.ReleaseComObject(workbooks);
                    }

                    if (excelApp != null)
                    {
                        try
                        {
                            excelApp.GetType().InvokeMember("Quit", BindingFlags.InvokeMethod, null, excelApp, null);
                        }
                        catch { }
                        Marshal.ReleaseComObject(excelApp);
                    }
                }
            }, ct);
        }
        finally
        {
            if (tempDirToClean != null && Directory.Exists(tempDirToClean))
            {
                try { Directory.Delete(tempDirToClean, true); } catch { }
            }
        }
    }
}
