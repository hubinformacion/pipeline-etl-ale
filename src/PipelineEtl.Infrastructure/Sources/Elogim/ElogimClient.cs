using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
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
            // El modo explícito respeta la configuración de TI; en remoto el valor predeterminado verifica TLS.
            if (!options.ContainsKey("SSL Mode") && options.Server is not ("localhost" or "127.0.0.1" or "::1"))
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
            throw DescribeError(ex, "inspeccionar");
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
            throw DescribeError(ex, "consultar");
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
                    throw DescribeError(ex, "completar la lectura");
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
        SELECT fecha_sesion, id_est AS user_email, identificacion, bd,
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

    private static InvalidOperationException DescribeError(MySqlException error, string operation)
    {
        // Traducimos solo causas conocidas; los mensajes originales pueden contener datos de conexión.
        var detail = error.Number switch
        {
            1042 => "No se estableció la conexión. Verifica servidor, puerto y acceso desde WSL (incluida VPN o túnel SSH, si corresponde).",
            1045 => "El servidor rechazó la autenticación. Revisa usuario, contraseña y autorización de la IP de origen.",
            1044 or 1142 => "El usuario no tiene permiso para consultar la base o la tabla.",
            1049 => "La base de datos configurada no existe o no es accesible.",
            1146 => "No se encontró la tabla configurada.",
            1054 => "La tabla no contiene alguna de las columnas requeridas por la consulta.",
            _ => "La operación no se completó. Revisa conexión y permisos SELECT."
        };
        if (error.Message.Equals("Server does not support SSL", StringComparison.OrdinalIgnoreCase))
            detail = "El servidor respondió, pero no anuncia soporte TLS. El modo SSL configurado exige TLS; compáralo con la conexión que funciona en Dataflare.";
        else if (error.Message.Equals("SSL Authentication Error", StringComparison.OrdinalIgnoreCase) ||
                 HasAuthenticationError(error))
            detail = "Falló la negociación TLS o la validación del certificado. Usa el hostname del certificado y configura SslCa si la CA es privada.";
        else if (error.Message.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
            detail = "Se agotó el tiempo de conexión. Revisa puerto, firewall, VPN y acceso desde WSL.";

        return new InvalidOperationException($"No se pudo {operation} Elogim/MariaDB. Código: {error.Number}. {detail}");
    }

    private static bool HasAuthenticationError(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is AuthenticationException) return true;
        return false;
    }

    private static string Quote(string identifier) => "`" + identifier.Replace("`", "``") + "`";
}
