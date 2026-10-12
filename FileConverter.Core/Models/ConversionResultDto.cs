namespace FileConverter.Core.Models;

public class ConversionResultDto
{
    public string JobId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> GeneratedFilePaths { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public long DurationMs { get; set; }
    public int ProcessedFilesCount { get; set; }
}

public class ProgressReportDto
{
    public string JobId { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
}
