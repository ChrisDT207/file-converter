using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class WordConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly IFileSystemService _fileSystemService;
    private readonly IDocumentConversionService? _documentService;
    private readonly ILogger<WordConverter> _logger;

    private static readonly HashSet<string> SupportedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "docx", "doc", "docm", "dotx", "dotm", "rtf", "odt", "txt", "html", "htm", "md", "pdf"
    };

    private static readonly HashSet<string> SupportedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "docx", "rtf", "odt", "html", "txt"
    };

    public WordConverter(
        IBinaryLocator binaryLocator,
        IFileSystemService fileSystemService,
        ILogger<WordConverter> logger,
        IDocumentConversionService? documentService = null)
    {
        _binaryLocator = binaryLocator;
        _fileSystemService = fileSystemService;
        _documentService = documentService;
        _logger = logger;
    }

    public string Name => "Microsoft Word Native Engine";
    public int Priority => 98;

    public bool IsAvailable()
    {
        return _binaryLocator.IsBinaryAvailable("winword") || _binaryLocator.IsBinaryAvailable("word");
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        if (!IsAvailable()) return false;

        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        if (src is "md" or "pdf")
        {
            return tgt == "odt";
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

        if (srcExt is "md" or "pdf" && _documentService != null)
        {
            tempDirToClean = Path.Combine(Path.GetTempPath(), "FC_WordTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirToClean);
            fileToOpen = await _documentService.ConvertDocumentAsync(sourcePath, "docx", tempDirToClean, ct);
        }

        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null)
                {
                    throw new InvalidOperationException("Microsoft Word is not installed or Word.Application COM is unavailable.");
                }

                object? wordApp = null;
                object? docs = null;
                object? doc = null;

                try
                {
                    wordApp = Activator.CreateInstance(wordType);
                    if (wordApp == null)
                    {
                        throw new InvalidOperationException("Failed to instantiate Word.Application COM object.");
                    }

                    // Visible = false, DisplayAlerts = 0 (wdAlertsNone)
                    wordApp.GetType().InvokeMember("Visible", BindingFlags.SetProperty, null, wordApp, new object[] { false });
                    wordApp.GetType().InvokeMember("DisplayAlerts", BindingFlags.SetProperty, null, wordApp, new object[] { 0 });

                    progress?.Report(35);
                    ct.ThrowIfCancellationRequested();

                    docs = wordApp.GetType().InvokeMember("Documents", BindingFlags.GetProperty, null, wordApp, null);
                    if (docs == null)
                    {
                        throw new InvalidOperationException("Failed to get Documents collection from Word.");
                    }

                    // Open(FileName, ConfirmConversions: 0, ReadOnly: -1, AddToRecentFiles: 0)
                    var openArgs = new object[] { Path.GetFullPath(fileToOpen), false, true, false };
                    doc = docs.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, docs, openArgs);
                    if (doc == null)
                    {
                        throw new InvalidOperationException($"Word failed to open '{fileToOpen}'.");
                    }

                    progress?.Report(65);
                    ct.ThrowIfCancellationRequested();

                    if (File.Exists(expectedOutput)) File.Delete(expectedOutput);

                    // wdFormatDocumentDefault = 16, wdFormatPDF = 17, wdFormatRTF = 6, wdFormatOpenDocumentText = 23, wdFormatHTML = 8, wdFormatUnicodeText = 7
                    int saveFormat = cleanTarget switch
                    {
                        "pdf" => 17,
                        "docx" => 16,
                        "rtf" => 6,
                        "odt" => 23,
                        "html" => 8,
                        "txt" => 7,
                        _ => 17
                    };

                    // SaveAs2(FileName, FileFormat)
                    var saveArgs = new object[] { Path.GetFullPath(expectedOutput), saveFormat };
                    doc.GetType().InvokeMember("SaveAs2", BindingFlags.InvokeMethod, null, doc, saveArgs);

                    progress?.Report(95);

                    if (!File.Exists(expectedOutput))
                    {
                        throw new FileNotFoundException($"Expected Word output file was not created: '{expectedOutput}'", expectedOutput);
                    }

                    return expectedOutput;
                }
                finally
                {
                    if (doc != null)
                    {
                        try
                        {
                            // Close(SaveChanges: 0 (wdDoNotSaveChanges))
                            doc.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, doc, new object[] { 0 });
                        }
                        catch { }
                        Marshal.ReleaseComObject(doc);
                    }

                    if (docs != null)
                    {
                        Marshal.ReleaseComObject(docs);
                    }

                    if (wordApp != null)
                    {
                        try
                        {
                            // Quit(SaveChanges: 0 (wdDoNotSaveChanges))
                            wordApp.GetType().InvokeMember("Quit", BindingFlags.InvokeMethod, null, wordApp, new object[] { 0 });
                        }
                        catch { }
                        Marshal.ReleaseComObject(wordApp);
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
