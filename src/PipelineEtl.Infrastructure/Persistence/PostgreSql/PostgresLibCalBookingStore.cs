using Npgsql;
using NpgsqlTypes;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

public sealed class PostgresLibCalBookingStore(string connectionString) : ILibCalBookingStore, IAsyncDisposable
{
    private const string BookingColumns = """
        extraction_date, source_row_id, booking_id, category_id, booking_category, campus_id, booking_campus,
        booking_starts_at, booking_ends_at, user_email, user_name, user_lastname, booking_resource,
        booking_status, booking_confirmation, booking_phone, booking_participant_2_email, booking_participant_3_email,
        booking_note
        """;
    private readonly PostgresDatabase database = new(connectionString);

    public async Task<long> ReplaceDayAsync(
        DateOnly date, IAsyncEnumerable<LibCalBookingPage> pages, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("SET LOCAL lock_timeout = '30s'", connection, transaction))
                await command.ExecuteNonQueryAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(51003, $1)", connection, transaction))
            {
                command.Parameters.AddWithValue(date.DayNumber);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var command = new NpgsqlCommand(
                "DELETE FROM etl.libcal_bookings WHERE extraction_date = $1", connection, transaction))
            {
                command.Parameters.AddWithValue(date);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            long count = 0;
            await using (var importer = await connection.BeginBinaryImportAsync(
                $"COPY etl.libcal_bookings ({BookingColumns}) FROM STDIN (FORMAT BINARY)", cancellationToken))
            {
                await foreach (var page in pages.WithCancellation(cancellationToken))
                foreach (var booking in page.Bookings)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await WriteBookingAsync(importer, date, page.Category, booking, cancellationToken);
                    count++;
                }
                await importer.CompleteAsync(cancellationToken);
            }
            await PostgresLoadStatus.RecordAsync(connection, transaction, "etl.libcal_bookings", date, count, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return count;
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException("LibCal: no se pudo confirmar la foto PostgreSQL. Revisa conexión, permisos y esquema. Código: " + (ex.SqlState ?? "conexión") + ". La foto anterior se conserva si la transacción no se confirmó.");
        }
    }

    public async Task<(long InsertedRows, long InsertedDays, long ProtectedDays)> ImportMissingDaysAsync(
        DateOnly firstDay, DateOnly lastDay, IEnumerable<(LibCalCategory Category, LibCalBooking Booking)> rows,
        CancellationToken cancellationToken)
    {
        if (lastDay < firstDay)
            throw new ArgumentException("El fin del histórico debe ser igual o posterior al inicio.");

        try
        {
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var prepare = new NpgsqlCommand("""
                SET LOCAL lock_timeout = '15s';
                CREATE TEMP TABLE libcal_history_stage
                    (LIKE etl.libcal_bookings INCLUDING CONSTRAINTS) ON COMMIT DROP;
                ALTER TABLE pg_temp.libcal_history_stage ALTER COLUMN source_row_id DROP NOT NULL;
                """, connection, transaction))
                await prepare.ExecuteNonQueryAsync(cancellationToken);

            var days = new HashSet<DateOnly>();
            await using (var importer = await connection.BeginBinaryImportAsync(
                $"COPY pg_temp.libcal_history_stage ({BookingColumns}) FROM STDIN (FORMAT BINARY)", cancellationToken))
            {
                foreach (var (category, booking) in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var date = DateOnly.FromDateTime(booking.StartsAt.DateTime);
                    if (date < firstDay || date > lastDay)
                        throw new InvalidOperationException("LibCal histórico: hay una reserva fuera del período solicitado.");
                    days.Add(date);
                    await WriteBookingAsync(importer, date, category, booking, cancellationToken);
                }

                if (days.Count != lastDay.DayNumber - firstDay.DayNumber + 1)
                    throw new InvalidOperationException("LibCal histórico: los CSV no contienen filas para todos los días solicitados.");
                await importer.CompleteAsync(cancellationToken);
            }

            // Seleccionar los días después del COPY, bajo un lock que excluye cualquier reemplazo nocturno.
            await using (var plan = new NpgsqlCommand("""
                LOCK TABLE etl.libcal_bookings IN ACCESS EXCLUSIVE MODE;
                CREATE TEMP TABLE libcal_history_days ON COMMIT DROP AS
                SELECT stage.extraction_date, count(*) AS row_count
                FROM pg_temp.libcal_history_stage stage
                WHERE NOT EXISTS (
                    SELECT 1 FROM etl.libcal_bookings existing
                    WHERE existing.extraction_date = stage.extraction_date
                ) AND NOT EXISTS (
                    SELECT 1 FROM etl.load_status status
                    WHERE status.dataset = 'etl.libcal_bookings' AND status.extraction_date = stage.extraction_date
                )
                GROUP BY stage.extraction_date;
                """, connection, transaction))
                await plan.ExecuteNonQueryAsync(cancellationToken);

            (long ExpectedRows, long Days) target;
            await using (var measure = new NpgsqlCommand("""
                SELECT coalesce(sum(row_count), 0)::bigint, count(*) FROM pg_temp.libcal_history_days
                """, connection, transaction))
            await using (var reader = await measure.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                target = (reader.GetInt64(0), reader.GetInt64(1));
            }

            if (target.Days > 0)
            {
                await using var insert = new NpgsqlCommand($"""
                    ALTER TABLE etl.libcal_bookings ALTER COLUMN source_row_id DROP NOT NULL;
                    INSERT INTO etl.libcal_bookings ({BookingColumns})
                    SELECT {BookingColumns} FROM pg_temp.libcal_history_stage
                    WHERE extraction_date IN (SELECT extraction_date FROM pg_temp.libcal_history_days);
                    """, connection, transaction);
                var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
                if (inserted != target.ExpectedRows)
                    throw new InvalidOperationException("LibCal histórico: el total insertado difiere del CSV.");
            }

            await transaction.CommitAsync(cancellationToken);
            return (target.ExpectedRows, target.Days, days.Count - target.Days);
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException("LibCal histórico: no se confirmó la importación en PostgreSQL. Código: " +
                                                (ex.SqlState ?? "conexión") + ".");
        }
    }

    private static async Task WriteBookingAsync(
        NpgsqlBinaryImporter importer, DateOnly date, LibCalCategory category, LibCalBooking booking,
        CancellationToken cancellationToken)
    {
        await importer.StartRowAsync(cancellationToken);
        await importer.WriteAsync(date, NpgsqlDbType.Date, cancellationToken);
        if (booking.RowId is null) await importer.WriteNullAsync(cancellationToken);
        else await importer.WriteAsync(booking.RowId, NpgsqlDbType.Text, cancellationToken);
        foreach (var value in new[] { booking.BookingId, category.Id, category.Name, category.LocationId,
                     category.LocationName })
            await importer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(booking.StartsAt.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(booking.EndsAt.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
        foreach (var value in new[] { booking.UserEmail, booking.UserName, booking.UserLastname,
                     booking.Resource, booking.Status, booking.Confirmation, booking.Phone,
                     booking.Participant2Email, booking.Participant3Email, booking.Note })
        {
            if (value is null) await importer.WriteNullAsync(cancellationToken);
            else await importer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);
        }
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
