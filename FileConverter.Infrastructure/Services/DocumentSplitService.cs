using FileConverter.Core.Interfaces;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace FileConverter.Infrastructure.Services;

public class DocumentSplitService : IDocumentSplitService
{
    private readonly IFileSystemService _fileSystemService;

    public DocumentSplitService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public Task<List<string>> SplitPdfToSinglePagesAsync(string pdfPath, string outputDir, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("PDF file not found", pdfPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var outputFiles = new List<string>();

            using var inputDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            int totalPages = inputDoc.PageCount;

            for (int i = 0; i < totalPages; i++)
            {
                ct.ThrowIfCancellationRequested();
                using var singleDoc = new PdfDocument();
                singleDoc.AddPage(inputDoc.Pages[i]);

                var dest = _fileSystemService.GetUniqueDestinationFilePath(
                    outputDir, 
                    $"{baseName}_page_{i + 1:D3}", 
                    ".pdf"
                );

                singleDoc.Save(dest);
                outputFiles.Add(dest);
            }

            return outputFiles;
        }, ct);
    }

    public Task<List<string>> SplitPdfByRangesAsync(string pdfPath, string outputDir, string rangeExpression, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("PDF file not found", pdfPath);
            }

            _fileSystemService.EnsureDirectoryExists(outputDir);
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var outputFiles = new List<string>();

            using var inputDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            int totalPages = inputDoc.PageCount;

            var segments = ParseRangeExpression(rangeExpression, totalPages);
            if (segments.Count == 0)
            {
                throw new ArgumentException("No valid page ranges could be resolved from expression: " + rangeExpression);
            }

            int partIndex = 1;
            foreach (var segment in segments)
            {
                ct.ThrowIfCancellationRequested();
                using var partDoc = new PdfDocument();
                foreach (var pageNum in segment.PageNumbers)
                {
                    partDoc.AddPage(inputDoc.Pages[pageNum - 1]);
                }

                var label = segment.IsRange 
                    ? $"{baseName}_part{partIndex}_pages_{segment.Start}-{segment.End}" 
                    : $"{baseName}_part{partIndex}_page_{segment.Start}";

                var dest = _fileSystemService.GetUniqueDestinationFilePath(outputDir, label, ".pdf");
                partDoc.Save(dest);
                outputFiles.Add(dest);
                partIndex++;
            }

            return outputFiles;
        }, ct);
    }

    private class RangeSegment
    {
        public bool IsRange { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public List<int> PageNumbers { get; set; } = new();
    }

    private static List<RangeSegment> ParseRangeExpression(string expression, int maxPage)
    {
        var result = new List<RangeSegment>();
        if (string.IsNullOrWhiteSpace(expression)) return result;

        var tokens = expression.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawToken in tokens)
        {
            var token = rawToken.Trim();
            if (token.Contains('-'))
            {
                var parts = token.Split('-');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0].Trim(), out int start) &&
                    int.TryParse(parts[1].Trim(), out int end))
                {
                    start = Math.Max(1, Math.Min(start, maxPage));
                    end = Math.Max(1, Math.Min(end, maxPage));
                    if (start > end) (start, end) = (end, start);

                    var pages = new List<int>();
                    for (int p = start; p <= end; p++) pages.Add(p);

                    result.Add(new RangeSegment
                    {
                        IsRange = true,
                        Start = start,
                        End = end,
                        PageNumbers = pages
                    });
                }
            }
            else if (int.TryParse(token, out int singlePage))
            {
                if (singlePage >= 1 && singlePage <= maxPage)
                {
                    result.Add(new RangeSegment
                    {
                        IsRange = false,
                        Start = singlePage,
                        End = singlePage,
                        PageNumbers = new List<int> { singlePage }
                    });
                }
            }
        }
        return result;
    }
}
