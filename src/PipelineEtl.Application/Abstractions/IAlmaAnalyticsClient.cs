using PipelineEtl.Domain;

namespace PipelineEtl.Application.Abstractions;

public interface IAlmaAnalyticsClient
{
    IAsyncEnumerable<AnalyticsReportPage> ReadPagesAsync(
        string report, DateOnly date, CancellationToken cancellationToken);

    Task<AnalyticsReportPreview> InspectAsync(string report, CancellationToken cancellationToken);
}
