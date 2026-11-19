using System.Runtime.CompilerServices;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public sealed class ExtractLibCalBookings(ILibCalClient client, IReadOnlyList<LibCalCategory> categories)
{
    public async IAsyncEnumerable<LibCalInspection> InspectAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateCategories();
        foreach (var category in categories)
            yield return await client.InspectAsync(category, date, cancellationToken);
    }

    public async IAsyncEnumerable<LibCalExtractionProgress> RunAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long rows = 0;
        await foreach (var page in ValidatedPagesAsync(date, cancellationToken))
        {
            if (page.Page == 1) rows = 0;
            rows += page.Bookings.Count;
            yield return new LibCalExtractionProgress(page.Category, page.Page, rows, page.IsFinished);
        }
    }

    public Task<long> LoadAsync(DateOnly date, ILibCalBookingStore store, CancellationToken cancellationToken) =>
        store.ReplaceDayAsync(date, ValidatedPagesAsync(date, cancellationToken), cancellationToken);

    private async IAsyncEnumerable<LibCalBookingPage> ValidatedPagesAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateCategories();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(ExtractionPlanner.TimeZoneId);
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var nextMidnight = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
        var end = new DateTimeOffset(nextMidnight, zone.GetUtcOffset(nextMidnight));
        foreach (var category in categories)
        {
            int pages = 0;
            bool finished = false;
            await foreach (var page in client.ReadPagesAsync(category, date, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (finished || page.Category != category || page.Page != pages + 1)
                    throw new InvalidOperationException("LibCal: secuencia de páginas inválida; extracción incompleta.");
                foreach (var booking in page.Bookings)
                {
                    if (booking.EndsAt <= booking.StartsAt || booking.StartsAt >= end || booking.EndsAt <= start)
                        throw new InvalidOperationException($"LibCal/categoría {category.Id}: una reserva no corresponde al día de uso solicitado.");
                }
                pages++;
                finished = page.IsFinished;
                yield return page;
            }
            if (!finished)
                throw new InvalidOperationException($"LibCal/categoría {category.Id}: extracción incompleta.");
        }
    }

    private void ValidateCategories()
    {
        if (categories.Count == 0 || categories.Select(c => c.Id).Distinct().Count() != categories.Count)
            throw new ArgumentException("LibCal: configura categorías no vacías y sin identificadores duplicados.");
    }
}
