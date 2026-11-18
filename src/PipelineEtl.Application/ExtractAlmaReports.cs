using System.Globalization;
using System.Runtime.CompilerServices;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public sealed class ExtractAlmaReports(IAlmaAnalyticsClient client)
{
    private static readonly string[] Reports = ["Loans", "Renewals"];

    public async IAsyncEnumerable<AlmaExtractionProgress> RunAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var report in Reports)
        {
            long rows = 0;
            int pages = 0;
            await foreach (var page in ValidatedPagesAsync(report, date, cancellationToken))
            {
                rows += page.Rows.Count;
                pages++;
                yield return new AlmaExtractionProgress(report, date, page.Columns.Count, pages, rows, page.IsFinished);
            }
        }
    }

    public async IAsyncEnumerable<AlmaExtractionProgress> LoadAsync(
        DateOnly date, IAlmaReportStore store, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var report in Reports)
        {
            yield return await store.ReplaceDayAsync(report, date,
                ValidatedPagesAsync(report, date, cancellationToken), cancellationToken);
        }
    }

    private async IAsyncEnumerable<AnalyticsReportPage> ValidatedPagesAsync(
        string report, DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool finished = false;
        await foreach (var page in client.ReadPagesAsync(report, date, cancellationToken))
        {
            if (finished || page.Report != report)
                throw new InvalidOperationException("Secuencia de páginas inválida; extracción cancelada.");
            var heading = report == "Loans" ? "Loan Date" : "Renewal Date";
            var candidates = page.Columns.Where(c => c.Heading == heading).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException($"Alma/{report}: no se identificó una única columna {heading}.");
            foreach (var row in page.Rows)
            {
                if (!row.TryGetValue(candidates[0].Id, out var value) ||
                    !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var actualDate) || actualDate != date)
                    throw new InvalidOperationException(
                        $"Alma/{report}: una fila no corresponde al día solicitado. Revisa el filtro del reporte.");
            }
            finished = page.IsFinished;
            yield return page;
        }
        if (!finished)
            throw new InvalidOperationException($"Alma/{report}: extracción incompleta.");
    }
}
