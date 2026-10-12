using FileConverter.Core.Models;

namespace FileConverter.Core.Interfaces;

public interface IConversionOrchestrator
{
    Task<ConversionResultDto> ExecuteConversionAsync(
        ConversionRequestDto request, 
        IProgress<ProgressReportDto>? progress = null, 
        CancellationToken ct = default);
}
