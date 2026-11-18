using PipelineEtl.Domain;

namespace PipelineEtl.Application.Abstractions;

public interface IElogimSessionStore
{
    Task<long> ReplaceDayAsync(DateOnly date, IAsyncEnumerable<ElogimSession> sessions,
        CancellationToken cancellationToken);
}
