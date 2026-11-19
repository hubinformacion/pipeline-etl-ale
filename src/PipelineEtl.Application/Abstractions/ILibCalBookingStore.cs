using PipelineEtl.Domain;

namespace PipelineEtl.Application.Abstractions;

public interface ILibCalBookingStore
{
    Task<long> ReplaceDayAsync(DateOnly date, IAsyncEnumerable<LibCalBookingPage> pages,
        CancellationToken cancellationToken);
}
