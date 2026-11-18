using PipelineEtl.Domain;

namespace PipelineEtl.Application.Abstractions;

public interface ILibCalClient
{
    Task<LibCalInspection> InspectAsync(LibCalCategory category, DateOnly date, CancellationToken cancellationToken);
    IAsyncEnumerable<LibCalBookingPage> ReadPagesAsync(
        LibCalCategory category, DateOnly date, CancellationToken cancellationToken);
}
