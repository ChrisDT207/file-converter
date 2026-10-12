using FileConverter.Core.Interfaces;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace FileConverter.Infrastructure.Services;

public class DocumentMergeService : IDocumentMergeService
{
    private readonly IFileSystemService _fileSystemService;

    public DocumentMergeService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public Task<string> MergePdfDocumentsAsync(IEnumerable<string> pdfPaths, string outputPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var fileList = pdfPaths.ToList();
            if (fileList.Count < 2)
            {
                throw new InvalidOperationException("At least 2 PDF documents are required to perform a merge.");
            }

            var dir = Path.GetDirectoryName(outputPath) ?? _fileSystemService.GetDefaultOutputDirectory();
            _fileSystemService.EnsureDirectoryExists(dir);

            using var outputDocument = new PdfDocument();

            foreach (var file in fileList)
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(file))
                {
                    throw new FileNotFoundException($"Cannot merge non-existent file: {file}");
                }

                using var inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                int count = inputDocument.PageCount;
                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var page = inputDocument.Pages[i];
                    outputDocument.AddPage(page);
                }
            }

            outputDocument.Save(outputPath);
            return outputPath;
        }, ct);
    }
}
