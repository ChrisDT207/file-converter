namespace FileConverter.Core.Models;

public class FileItemDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "pdf", "word", "image", "text", "other"
}
