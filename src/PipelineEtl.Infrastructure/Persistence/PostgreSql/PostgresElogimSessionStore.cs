using Npgsql;
using NpgsqlTypes;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

public sealed class PostgresElogimSessionStore(string connectionString) : IElogimSessionStore, IAsyncDisposable
{
    private readonly PostgresDatabase database = new(connectionString);

    public async Task<long> ReplaceDayAsync(
        DateOnly date, IAsyncEnumerable<ElogimSession> sessions, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("SET LOCAL lock_timeout = '30s'", connection, transaction))
                await command.ExecuteNonQueryAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(51002, $1)", connection, transaction))
            {
                command.Parameters.AddWithValue(date.DayNumber);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var command = new NpgsqlCommand(
                "DELETE FROM etl.elogim_sessions WHERE extraction_date = $1", connection, transaction))
            {
                command.Parameters.AddWithValue(date);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            long count = 0;
            var loadedAt = DateTime.UtcNow;
            await using (var importer = await connection.BeginBinaryImportAsync("""
                COPY etl.elogim_sessions (extraction_date, row_number, loaded_at,
                    fecha_sesion, user_email, identificacion, bd, tipo_consulta)
                FROM STDIN (FORMAT BINARY)
                """, cancellationToken))
            {
                await foreach (var session in sessions.WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(date, NpgsqlDbType.Date, cancellationToken);
                    await importer.WriteAsync(++count, NpgsqlDbType.Bigint, cancellationToken);
                    await importer.WriteAsync(loadedAt, NpgsqlDbType.TimestampTz, cancellationToken);
                    await importer.WriteAsync(session.FechaSesion, NpgsqlDbType.Timestamp, cancellationToken);
                    foreach (var value in new[] { session.UserEmail, session.Identificacion, session.Bd, session.TipoConsulta })
                    {
                        if (value is null) await importer.WriteNullAsync(cancellationToken);
                        else await importer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);
                    }
                }
                await importer.CompleteAsync(cancellationToken);
            }
            await PostgresLoadStatus.RecordAsync(connection, transaction, "etl.elogim_sessions", date, count, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return count;
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException("Elogim: no se pudo confirmar la carga PostgreSQL. Revisa conexión, permisos y esquema; puedes repetir el día completo. Código: " + (ex.SqlState ?? "conexión") + ".");
        }
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
