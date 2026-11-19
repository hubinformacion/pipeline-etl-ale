using Npgsql;
using NpgsqlTypes;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

public sealed class PostgresAlmaReportStore : IAlmaReportStore, IAsyncDisposable
{
    private readonly PostgresDatabase database;
    private NpgsqlDataSource dataSource => database.DataSource;

    public PostgresAlmaReportStore(string connectionString) => database = new PostgresDatabase(connectionString);

    public async Task<AlmaExtractionProgress> ReplaceDayAsync(
        string report, DateOnly date, IAsyncEnumerable<AnalyticsReportPage> pages,
        CancellationToken cancellationToken)
    {
        var mapping = AlmaTableMapping.For(report);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            // El lock transaccional serializa cargas de la misma fuente/día, también con PgBouncer.
            await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '30s'", connection, transaction))
                await timeout.ExecuteNonQueryAsync(cancellationToken);
            await using (var command = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(51001, $1)", connection, transaction))
            {
                command.Parameters.AddWithValue(date.DayNumber * 2 + (report == "Loans" ? 0 : 1));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var command = new NpgsqlCommand(
                $"DELETE FROM {mapping.Table} WHERE extraction_date = $1", connection, transaction))
            {
                command.Parameters.AddWithValue(date);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            long rowCount = 0;
            int pageCount = 0;
            bool finished = false;
            var loadedAt = DateTime.UtcNow;
            var columnNames = string.Join(", ", mapping.Columns.Select(c => c.Name));
            await foreach (var page in pages.WithCancellation(cancellationToken))
            {
                if (finished || page.Report != report)
                    throw new InvalidOperationException("Secuencia de páginas inválida; carga cancelada.");
                mapping.ValidateSchema(page.Columns);
                // COPY reduce viajes de red: se escribe una página cada vez, sin acumular el reporte.
                await using var importer = await connection.BeginBinaryImportAsync(
                    $"COPY {mapping.Table} (extraction_date, row_number, loaded_at, {columnNames}) FROM STDIN (FORMAT BINARY)", cancellationToken);
                foreach (var row in page.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(date, NpgsqlDbType.Date, cancellationToken);
                    await importer.WriteAsync(++rowCount, NpgsqlDbType.Bigint, cancellationToken);
                    await importer.WriteAsync(loadedAt, NpgsqlDbType.TimestampTz, cancellationToken);
                    for (int i = 0; i < mapping.Columns.Count; i++)
                    {
                        var sourceColumn = AlmaTableMapping.SourceColumnId(i);
                        if (!row.TryGetValue(sourceColumn, out var raw))
                            throw new InvalidOperationException($"Alma/{report}: faltó {sourceColumn}; carga cancelada.");
                        var value = mapping.ConvertValue(i, raw);
                        if (value is null) await importer.WriteNullAsync(cancellationToken);
                        else await importer.WriteAsync(value, mapping.Columns[i].DbType, cancellationToken);
                    }
                }
                await importer.CompleteAsync(cancellationToken);
                pageCount++;
                finished = page.IsFinished;
            }
            if (!finished)
                throw new InvalidOperationException($"Alma/{report}: extracción incompleta; no se confirmará la carga.");

            await transaction.CommitAsync(cancellationToken);
            return new AlmaExtractionProgress(report, date, mapping.Columns.Count, pageCount, rowCount, true);
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException($"Alma/{report}: no se pudo confirmar la carga PostgreSQL. Revisa conexión, permisos y esquema; puedes repetir el día completo. Código: " + (ex.SqlState ?? "conexión") + ".");
        }
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
