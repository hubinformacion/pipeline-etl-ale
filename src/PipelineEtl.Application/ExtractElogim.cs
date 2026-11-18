using PipelineEtl.Application.Abstractions;

namespace PipelineEtl.Application;

public sealed class ExtractElogim(IElogimClient client)
{
    public async Task<long> RunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        long count = 0;
        await foreach (var session in client.ReadAsync(date, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateOnly.FromDateTime(session.FechaSesion) != date)
                throw new InvalidOperationException("Elogim devolvió una fila fuera del día solicitado; extracción cancelada.");
            count++;
        }
        return count;
    }
}
