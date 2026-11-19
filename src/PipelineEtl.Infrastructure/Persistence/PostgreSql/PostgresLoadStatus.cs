using Npgsql;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

internal static class PostgresLoadStatus
{
    public static async Task RecordAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string dataset, DateOnly date, long count, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO etl.load_status (dataset, extraction_date, row_count, loaded_at)
            VALUES ($1, $2, $3, clock_timestamp())
            ON CONFLICT (dataset, extraction_date) DO UPDATE
            SET row_count = EXCLUDED.row_count, loaded_at = EXCLUDED.loaded_at
            """, connection, transaction);
        command.Parameters.AddWithValue(dataset);
        command.Parameters.AddWithValue(date);
        command.Parameters.AddWithValue(count);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
