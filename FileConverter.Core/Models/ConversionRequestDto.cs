using FileConverter.Core.Enums;

namespace FileConverter.Core.Models;

public class ConversionItemDto
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string TargetExtension { get; set; } = string.Empty;
}

public class ConversionRequestDto
{
    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public FileActionType ActionType { get; set; } = FileActionType.ConvertToTargetFormat;
    public string TargetExtension { get; set; } = "pdf"; // Fallback target format if Items is empty
    public List<string> SourceFilePaths { get; set; } = new(); // Fallback files list if Items is empty
    public List<ConversionItemDto> Items { get; set; } = new(); // Itemized conversions for mixed batches
    public string OutputDirectory { get; set; } = string.Empty;
    public string? CustomOutputFileName { get; set; }
    public string? PageRangeExpression { get; set; } // For split operations (e.g. "1-3, 5, 7-10")
}
