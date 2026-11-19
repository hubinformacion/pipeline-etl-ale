using Npgsql;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

public sealed class PostgresDatabase : IAsyncDisposable
{
    internal NpgsqlDataSource DataSource { get; }

    public PostgresDatabase(string connectionString)
    {
        try
        {
            var options = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.Database) ||
                string.IsNullOrWhiteSpace(options.Username)) throw new ArgumentException();
            if (options.Host is not ("localhost" or "127.0.0.1" or "::1"))
                options.SslMode = SslMode.VerifyFull;
            options.IncludeErrorDetail = false;
            options.PersistSecurityInfo = false;
            DataSource = NpgsqlDataSource.Create(options.ConnectionString);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("NEON_CONNECTION_STRING debe usar formato .NET: Host=...;Database=...;Username=...;Password=... (no URL postgresql://).");
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            string[] scripts =
            [
                "001_alma_reports.sql",
                "002_elogim_sessions.sql",
                "003_libcal_bookings.sql",
                "005_libcal_canonical_columns.sql",
                "006_libcal_report_fields.sql",
                "007_libcal_remove_month_columns.sql",
                "008_alma_remove_source_column0.sql"
            ];
            foreach (var name in scripts)
            {
                using var stream = typeof(PostgresDatabase).Assembly.GetManifestResourceStream(
                    "PipelineEtl.Infrastructure.Persistence.PostgreSql." + name)
                    ?? throw new InvalidOperationException("No se encontró el script inicial de PostgreSQL.");
                using var reader = new StreamReader(stream);
                await using var command = new NpgsqlCommand(await reader.ReadToEndAsync(cancellationToken), connection, transaction);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException("No se pudo inicializar PostgreSQL. Revisa conexión, TLS y permisos de creación del esquema etl. Código: " + (ex.SqlState ?? "conexión") + ".");
        }
    }

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();
}
