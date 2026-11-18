using PipelineEtl.Domain;

namespace PipelineEtl.Application.Abstractions;

public interface IAlmaReportStore
{
    Task<AlmaExtractionProgress> ReplaceDayAsync(
        string report, DateOnly date, IAsyncEnumerable<AnalyticsReportPage> pages,
        CancellationToken cancellationToken);
}
