using System.Globalization;
using System.Runtime.CompilerServices;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public sealed class ExtractAlmaReports(IAlmaAnalyticsClient client)
{
    public async IAsyncEnumerable<AlmaExtractionProgress> RunAsync(
        DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var report in new[] { "Loans", "Renewals" })
        {
            long rows = 0;
            int pages = 0;
            await foreach (var page in client.ReadPagesAsync(report, date, cancellationToken))
            {
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

                rows += page.Rows.Count;
                pages++;
                // Por ahora validamos y contamos cada lote; la persistencia se incorporará después.
                yield return new AlmaExtractionProgress(report, date, page.Columns.Count, pages, rows, page.IsFinished);
            }
        }
    }
}
