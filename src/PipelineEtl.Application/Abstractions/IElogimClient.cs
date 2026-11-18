using PipelineEtl.Domain;

namespace PipelineEtl.Application.Abstractions;

public interface IElogimClient
{
    IAsyncEnumerable<ElogimSession> ReadAsync(DateOnly date, CancellationToken cancellationToken);

    Task<IReadOnlyList<ElogimColumn>> InspectAsync(CancellationToken cancellationToken);
}
