using System.Globalization;
using System.Runtime.CompilerServices;
using MySqlConnector;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Sources.Elogim;

public sealed class ElogimClient : IElogimClient
{
    private readonly string connectionString;
    private readonly string table;

    public ElogimClient(string connectionString, string table = "est_ezproxy")
    {
        try
        {
            var options = new MySqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(options.Server) || string.IsNullOrWhiteSpace(options.Database) ||
                string.IsNullOrWhiteSpace(options.UserID) || string.IsNullOrWhiteSpace(table))
                throw new ArgumentException();
            // Fuera de localhost verificamos identidad del servidor y cifrado de la conexión.
            if (options.Server is not ("localhost" or "127.0.0.1" or "::1"))
                options.SslMode = MySqlSslMode.VerifyFull;
            options.PersistSecurityInfo = false;
            options.AllowLoadLocalInfile = false;
            options.DateTimeKind = MySqlDateTimeKind.Unspecified;
            this.connectionString = options.ConnectionString;
            this.table = Quote(options.Database) + "." + Quote(table);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Configura ELOGIM_CONNECTION_STRING con Server=...;Port=3306;Database=...;User ID=...;Password=... y ELOGIM_TABLE si necesitas cambiar est_ezproxy.");
        }
    }

    public async Task<IReadOnlyList<ElogimColumn>> InspectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            // LIMIT 0 obtiene el esquema sin devolver registros personales.
            await using var command = CreateCommand(connection, "LIMIT 0");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return reader.GetColumnSchema().Select(column => new ElogimColumn(
                column.ColumnName, column.DataTypeName ?? "desconocido",
                column.DataType?.FullName ?? "desconocido", column.AllowDBNull)).ToArray();
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"No se pudo inspeccionar Elogim/MariaDB. Revisa conexión, TLS, permisos SELECT y tabla. Código: {ex.Number}.");
        }
    }

    public async IAsyncEnumerable<ElogimSession> ReadAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        await using var connection = new MySqlConnection(connectionString);
        await using var command = CreateCommand(connection,
            "WHERE fecha_sesion >= @start AND fecha_sesion < @end");
        command.Parameters.Add("@start", MySqlDbType.DateTime).Value = start;
        command.Parameters.Add("@end", MySqlDbType.DateTime).Value = end;
        MySqlDataReader reader;
        try
        {
            await connection.OpenAsync(cancellationToken);
            reader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"No se pudo consultar Elogim/MariaDB. Revisa conexión, TLS y permisos SELECT. Código: {ex.Number}.");
        }
        await using (reader)
        {
            while (true)
            {
                ElogimSession session;
                try
                {
                    if (!await reader.ReadAsync(cancellationToken)) break;
                    session = new ElogimSession(reader.GetDateTime(0), ReadText(reader, 1),
                        ReadText(reader, 2), ReadText(reader, 3), ReadText(reader, 4));
                }
                catch (MySqlException ex)
                {
                    throw new InvalidOperationException($"La lectura de Elogim no se completó. Código: {ex.Number}.");
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                {
                    throw new InvalidOperationException("Elogim devolvió un tipo o fecha no válido; revisa --inspect-elogim.");
                }
                yield return session;
            }
        }
    }

    private MySqlCommand CreateCommand(MySqlConnection connection, string condition) => new($"""
        SELECT fecha_sesion, id_est, identificacion, bd,
            CASE tipo_consulta
                WHEN 'pdf' THEN 'Descarga'
                WHEN 'busqueda' THEN 'Búsqueda'
                WHEN 'sesion' THEN 'Acceso'
                ELSE tipo_consulta
            END AS tipo_consulta
        FROM {table}
        {condition}
        """, connection) { CommandTimeout = 120 };

    private static string? ReadText(MySqlDataReader reader, int ordinal) => reader.GetValue(ordinal) switch
    {
        DBNull => null,
        string text => text,
        sbyte or byte or short or ushort or int or uint or long or ulong or decimal or float or double
            => Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("Elogim devolvió un tipo no admitido; revisa --inspect-elogim.")
    };

    private static string Quote(string identifier) => "`" + identifier.Replace("`", "``") + "`";
}
