using System.Runtime.CompilerServices;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public sealed class ExtractElogim(IElogimClient client)
{
    public async Task<long> RunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        long count = 0;
        await foreach (var session in ValidatedSessionsAsync(date, cancellationToken)) count++;
        return count;
    }

    public Task<long> LoadAsync(DateOnly date, IElogimSessionStore store, CancellationToken cancellationToken) =>
        store.ReplaceDayAsync(date, ValidatedSessionsAsync(date, cancellationToken), cancellationToken);

    private async IAsyncEnumerable<ElogimSession> ValidatedSessionsAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var session in client.ReadAsync(date, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateOnly.FromDateTime(session.FechaSesion) != date || session.FechaSesion.Kind != DateTimeKind.Unspecified)
                throw new InvalidOperationException("Elogim devolvió una fecha fuera del día solicitado o con zona horaria inesperada; extracción cancelada.");
            yield return session;
        }
    }
}
