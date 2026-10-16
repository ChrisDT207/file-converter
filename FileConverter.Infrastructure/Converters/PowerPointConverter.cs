using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class PowerPointConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<PowerPointConverter> _logger;

    private static readonly HashSet<string> SourceExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "pptx", "ppt", "pptm", "odp"
    };

    private static readonly HashSet<string> TargetExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "png", "jpg", "jpeg", "pptx", "odp", "html", "txt"
    };

    public PowerPointConverter(
        IBinaryLocator binaryLocator, 
        IFileSystemService fileSystemService,
        ILogger<PowerPointConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public string Name => "Microsoft PowerPoint Automation";
    public int Priority => 98;

    public bool IsAvailable() => _binaryLocator.IsBinaryAvailable("powerpnt");

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;
        return SourceExts.Contains(src) && TargetExts.Contains(tgt);
    }

    public async Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        string outputDir, 
        IProgress<int>? progress = null, 
        CancellationToken ct = default)
    {
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var expectedOutput = _fileSystemService.GetUniqueDestinationFilePath(outputDir, baseName, $".{cleanTarget}");

        _fileSystemService.EnsureDirectoryExists(outputDir);
        progress?.Report(15);

        _logger.LogInformation("Converting presentation via Microsoft PowerPoint COM: {File} -> {Target}", sourcePath, cleanTarget);

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var pptType = Type.GetTypeFromProgID("PowerPoint.Application");
            if (pptType == null)
            {
                throw new InvalidOperationException("PowerPoint.Application COM object could not be resolved.");
            }

            object? ppt = null;
            object? pres = null;

            try
            {
                ppt = Activator.CreateInstance(pptType);
                if (ppt == null)
                {
                    throw new InvalidOperationException("Failed to instantiate PowerPoint COM application.");
                }

                progress?.Report(35);
                ct.ThrowIfCancellationRequested();

                var presentations = pptType.InvokeMember("Presentations", BindingFlags.GetProperty, null, ppt, null);
                if (presentations == null)
                {
                    throw new InvalidOperationException("Failed to get Presentations collection from PowerPoint.");
                }

                // Open(FileName, ReadOnly: -1 (msoTrue), Untitled: 0 (msoFalse), WithWindow: 0 (msoFalse))
                var openArgs = new object[] { Path.GetFullPath(sourcePath), -1, 0, 0 };
                pres = presentations.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, presentations, openArgs);
                if (pres == null)
                {
                    throw new InvalidOperationException($"PowerPoint failed to open '{sourcePath}'.");
                }

                progress?.Report(65);
                ct.ThrowIfCancellationRequested();

                if (cleanTarget == "txt")
                {
                    // Extract text from all slide shapes
                    var sb = new StringBuilder();
                    var slides = pres.GetType().InvokeMember("Slides", BindingFlags.GetProperty, null, pres, null);
                    if (slides != null)
                    {
                        int slideCount = (int)(slides.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, slides, null) ?? 0);
                        for (int i = 1; i <= slideCount; i++)
                        {
                            var slide = slides.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, slides, new object[] { i });
                            if (slide == null) continue;

                            sb.AppendLine($"=== SLIDE {i} ===");
                            var shapes = slide.GetType().InvokeMember("Shapes", BindingFlags.GetProperty, null, slide, null);
                            if (shapes != null)
                            {
                                int shapeCount = (int)(shapes.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, shapes, null) ?? 0);
                                for (int s = 1; s <= shapeCount; s++)
                                {
                                    var shape = shapes.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, shapes, new object[] { s });
                                    if (shape == null) continue;

                                    var hasText = shape.GetType().InvokeMember("HasTextFrame", BindingFlags.GetProperty, null, shape, null);
                                    if (hasText != null && Convert.ToInt32(hasText) == -1) // -1 = msoTrue
                                    {
                                        var textFrame = shape.GetType().InvokeMember("TextFrame", BindingFlags.GetProperty, null, shape, null);
                                        var textRange = textFrame?.GetType().InvokeMember("TextRange", BindingFlags.GetProperty, null, textFrame, null);
                                        var text = textRange?.GetType().InvokeMember("Text", BindingFlags.GetProperty, null, textRange, null) as string;
                                        if (!string.IsNullOrWhiteSpace(text))
                                        {
                                            sb.AppendLine(text.Trim());
                                        }
                                        if (textRange != null) Marshal.ReleaseComObject(textRange);
                                        if (textFrame != null) Marshal.ReleaseComObject(textFrame);
                                    }
                                    Marshal.ReleaseComObject(shape);
                                }
                                Marshal.ReleaseComObject(shapes);
                            }
                            sb.AppendLine();
                            Marshal.ReleaseComObject(slide);
                        }
                        Marshal.ReleaseComObject(slides);
                    }

                    File.WriteAllText(expectedOutput, sb.ToString(), Encoding.UTF8);
                }
                else
                {
                    // ppSaveAsPDF = 32, ppSaveAsPNG = 17, ppSaveAsJPG = 16, ppSaveAsDefault (pptx) = 11, ppSaveAsOpenDocumentPresentation = 24, ppSaveAsHTML = 20
                    int saveFormat = cleanTarget switch
                    {
                        "pdf" => 32,
                        "png" => 17,
                        "jpg" or "jpeg" => 16,
                        "pptx" => 11,
                        "odp" => 24,
                        "html" => 20,
                        _ => 32
                    };

                    if (File.Exists(expectedOutput)) File.Delete(expectedOutput);

                    var saveArgs = new object[] { Path.GetFullPath(expectedOutput), saveFormat };
                    pres.GetType().InvokeMember("SaveAs", BindingFlags.InvokeMethod, null, pres, saveArgs);

                    // When exporting to PNG/JPG, PowerPoint might save slides into a directory named expectedOutput
                    if (Directory.Exists(expectedOutput))
                    {
                        var slideImages = Directory.GetFiles(expectedOutput, $"*.{cleanTarget}")
                            .OrderBy(f => f)
                            .ToList();

                        if (slideImages.Count > 0)
                        {
                            var primarySlide = Path.Combine(outputDir, $"{baseName}_slide1.{cleanTarget}");
                            File.Copy(slideImages[0], primarySlide, true);
                            expectedOutput = primarySlide;
                        }
                    }
                }

                progress?.Report(100);

                if (!File.Exists(expectedOutput) && !Directory.Exists(expectedOutput))
                {
                    throw new FileNotFoundException($"PowerPoint finished conversion but output '{expectedOutput}' was not created.");
                }

                return expectedOutput;
            }
            finally
            {
                if (pres != null)
                {
                    try { pres.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, pres, null); } catch { }
                    Marshal.ReleaseComObject(pres);
                }

                if (ppt != null)
                {
                    try { pptType.InvokeMember("Quit", BindingFlags.InvokeMethod, null, ppt, null); } catch { }
                    Marshal.ReleaseComObject(ppt);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }, ct);
    }
}
