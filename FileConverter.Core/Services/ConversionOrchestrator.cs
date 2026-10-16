using System.Diagnostics;
using FileConverter.Core.Enums;
using FileConverter.Core.Interfaces;
using FileConverter.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileConverter.Core.Services;

public class ConversionOrchestrator : IConversionOrchestrator
{
    private readonly IEnumerable<IFileConverter> _converters;
    private readonly IDocumentMergeService _documentMergeService;
    private readonly IDocumentSplitService _documentSplitService;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<ConversionOrchestrator> _logger;

    public ConversionOrchestrator(
        IEnumerable<IFileConverter> converters,
        IDocumentMergeService documentMergeService,
        IDocumentSplitService documentSplitService,
        IFileSystemService fileSystemService,
        ILogger<ConversionOrchestrator> logger)
    {
        _converters = converters;
        _documentMergeService = documentMergeService;
        _documentSplitService = documentSplitService;
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public async Task<ConversionResultDto> ExecuteConversionAsync(
        ConversionRequestDto request,
        IProgress<ProgressReportDto>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ConversionResultDto
        {
            JobId = request.JobId,
            Success = false
        };

        try
        {
            var outputDir = string.IsNullOrWhiteSpace(request.OutputDirectory)
                ? _fileSystemService.GetDefaultOutputDirectory()
                : _fileSystemService.EnsureDirectoryExists(request.OutputDirectory);

            var itemsToProcess = new List<ConversionItemDto>();
            if (request.Items != null && request.Items.Count > 0)
            {
                itemsToProcess = request.Items.Where(i => File.Exists(i.SourceFilePath)).ToList();
            }
            else
            {
                itemsToProcess = request.SourceFilePaths
                    .Where(File.Exists)
                    .Select(f => new ConversionItemDto { SourceFilePath = f, TargetExtension = request.TargetExtension })
                    .ToList();
            }

            if (itemsToProcess.Count == 0 && request.ActionType != FileActionType.MergeDocuments && request.ActionType != FileActionType.SplitDocument)
            {
                result.Message = "No valid files found for processing.";
                return result;
            }

            switch (request.ActionType)
            {
                case FileActionType.ConvertToTargetFormat:
                    await ProcessTargetConversionAsync(itemsToProcess, outputDir, request, result, progress, ct);
                    break;

                case FileActionType.MergeDocuments:
                    var mergeFiles = request.SourceFilePaths.Where(File.Exists).ToList();
                    await ProcessMergeDocumentsAsync(mergeFiles, outputDir, request, result, progress, ct);
                    break;

                case FileActionType.SplitDocument:
                    var splitFiles = request.SourceFilePaths.Where(File.Exists).ToList();
                    await ProcessSplitDocumentAsync(splitFiles, outputDir, request, result, progress, ct);
                    break;

                default:
                    throw new NotSupportedException($"Action '{request.ActionType}' is not supported.");
            }

            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Success = result.GeneratedFilePaths.Count > 0;
            result.ProcessedFilesCount = result.GeneratedFilePaths.Count;

            if (result.Success)
            {
                result.Message = $"Successfully converted {result.GeneratedFilePaths.Count} file(s) in {result.DurationMs}ms.";
            }
            else if (result.Errors.Count > 0)
            {
                result.Message = $"Conversion failed: {string.Join("; ", result.Errors)}";
            }
        }
        catch (OperationCanceledException)
        {
            result.Message = "Conversion operation was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing conversion for job {JobId}", request.JobId);
            result.Errors.Add(ex.Message);
            result.Message = $"Critical error during conversion: {ex.Message}";
        }

        return result;
    }

    private async Task ProcessTargetConversionAsync(
        List<ConversionItemDto> items,
        string outputDir,
        ConversionRequestDto request,
        ConversionResultDto result,
        IProgress<ProgressReportDto>? progress,
        CancellationToken ct)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[i];
            var file = item.SourceFilePath;
            var cleanTarget = item.TargetExtension.TrimStart('.').ToLowerInvariant();
            var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            var fileName = Path.GetFileName(file);

            int pct = (int)(((double)i / items.Count) * 100);
            progress?.Report(new ProgressReportDto
            {
                JobId = request.JobId,
                Percentage = pct,
                CurrentFile = fileName,
                StatusText = $"Converting {fileName} to {cleanTarget.ToUpper()} ({i + 1}/{items.Count})..."
            });

            // Find matching available converters ordered by priority descending
            var candidateConverters = _converters
                .Where(c => c.IsAvailable() && c.CanConvert(ext, cleanTarget))
                .OrderByDescending(c => c.Priority)
                .ToList();

            if (candidateConverters.Count == 0)
            {
                result.Errors.Add($"No available converter found for converting '{fileName}' (.{ext}) to .{cleanTarget}.");
                continue;
            }

            bool succeeded = false;
            string? lastError = null;
            foreach (var converter in candidateConverters)
            {
                try
                {
                    _logger.LogInformation("Converting {File} via {Converter}...", fileName, converter.Name);
                    var generatedPath = await converter.ConvertAsync(file, cleanTarget, outputDir, null, ct);
                    result.GeneratedFilePaths.Add(generatedPath);
                    succeeded = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    _logger.LogWarning(ex, "Converter '{Converter}' failed on {File}. Trying next candidate...", converter.Name, fileName);
                }
            }

            if (!succeeded)
            {
                result.Errors.Add($"Conversion failed for '{fileName}' to .{cleanTarget}" + (!string.IsNullOrWhiteSpace(lastError) ? $": {lastError}" : "."));
            }
        }

        progress?.Report(new ProgressReportDto
        {
            JobId = request.JobId,
            Percentage = 100,
            StatusText = $"Completed processing {result.GeneratedFilePaths.Count} files."
        });
    }

    private async Task ProcessMergeDocumentsAsync(
        List<string> files,
        string outputDir,
        ConversionRequestDto request,
        ConversionResultDto result,
        IProgress<ProgressReportDto>? progress,
        CancellationToken ct)
    {
        progress?.Report(new ProgressReportDto
        {
            JobId = request.JobId,
            Percentage = 20,
            StatusText = $"Preparing {files.Count} documents for merge..."
        });

        var pdfsToMerge = new List<string>();
        var tempPdfsToCleanup = new List<string>();

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var file = files[i];
                var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

                if (ext == "pdf")
                {
                    pdfsToMerge.Add(file);
                }
                else
                {
                    // Convert to temp PDF first using registered converters
                    var tempConverter = _converters
                        .Where(c => c.IsAvailable() && c.CanConvert(ext, "pdf"))
                        .OrderByDescending(c => c.Priority)
                        .FirstOrDefault();

                    if (tempConverter == null)
                    {
                        throw new InvalidOperationException($"Cannot convert '{Path.GetFileName(file)}' to PDF for merging: no converter available.");
                    }

                    var tempPdf = await tempConverter.ConvertAsync(file, "pdf", outputDir, null, ct);
                    pdfsToMerge.Add(tempPdf);
                    tempPdfsToCleanup.Add(tempPdf);
                }
            }

            var mergedFileName = string.IsNullOrWhiteSpace(request.CustomOutputFileName)
                ? $"Merged_Document_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                : request.CustomOutputFileName;

            if (!mergedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                mergedFileName += ".pdf";
            }

            var mergedPath = Path.Combine(outputDir, mergedFileName);

            progress?.Report(new ProgressReportDto
            {
                JobId = request.JobId,
                Percentage = 70,
                StatusText = "Merging pages into final PDF document..."
            });

            await _documentMergeService.MergePdfDocumentsAsync(pdfsToMerge, mergedPath, ct);
            result.GeneratedFilePaths.Add(mergedPath);

            progress?.Report(new ProgressReportDto
            {
                JobId = request.JobId,
                Percentage = 100,
                StatusText = $"Successfully merged {files.Count} files into 1 PDF."
            });
        }
        finally
        {
            foreach (var tempPdf in tempPdfsToCleanup)
            {
                try { if (File.Exists(tempPdf)) File.Delete(tempPdf); } catch { }
            }
        }
    }

    private async Task ProcessSplitDocumentAsync(
        List<string> files,
        string outputDir,
        ConversionRequestDto request,
        ConversionResultDto result,
        IProgress<ProgressReportDto>? progress,
        CancellationToken ct)
    {
        var targetPdf = files.FirstOrDefault();
        if (targetPdf == null || !File.Exists(targetPdf))
        {
            result.Message = "No PDF document selected for split operation.";
            return;
        }

        progress?.Report(new ProgressReportDto
        {
            JobId = request.JobId,
            Percentage = 30,
            StatusText = $"Splitting PDF document '{Path.GetFileName(targetPdf)}'..."
        });

        List<string> splitOutputs;

        if (string.IsNullOrWhiteSpace(request.PageRangeExpression))
        {
            splitOutputs = await _documentSplitService.SplitPdfToSinglePagesAsync(targetPdf, outputDir, ct);
        }
        else
        {
            splitOutputs = await _documentSplitService.SplitPdfByRangesAsync(targetPdf, outputDir, request.PageRangeExpression, ct);
        }

        result.GeneratedFilePaths.AddRange(splitOutputs);

        progress?.Report(new ProgressReportDto
        {
            JobId = request.JobId,
            Percentage = 100,
            StatusText = $"Split completed: generated {splitOutputs.Count} page files."
        });
    }
}
