using Npgsql;
using NpgsqlTypes;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

public sealed class PostgresLibCalBookingStore(string connectionString) : ILibCalBookingStore, IAsyncDisposable
{
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
            await using (var importer = await connection.BeginBinaryImportAsync("""
                COPY etl.libcal_bookings (extraction_date,
                    source_row_id, booking_id, category_id, booking_category, campus_id, booking_campus,
                    booking_starts_at, booking_ends_at, user_email, user_name, user_lastname, booking_resource,
                    booking_status, booking_confirmation, booking_phone, booking_participant_2_email, booking_participant_3_email,
                    booking_note)
                FROM STDIN (FORMAT BINARY)
                """, cancellationToken))
            {
                await foreach (var page in pages.WithCancellation(cancellationToken))
                foreach (var booking in page.Bookings)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(date, NpgsqlDbType.Date, cancellationToken);
                    foreach (var value in new[] { booking.RowId, booking.BookingId, page.Category.Id,
                                 page.Category.Name, page.Category.LocationId, page.Category.LocationName })
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

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
