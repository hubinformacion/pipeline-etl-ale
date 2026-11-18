using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Sources.Alma;

public sealed class AlmaAnalyticsClient(HttpClient httpClient, string apiKey) : IAlmaAnalyticsClient
{
    private const string BaseUrl = "https://api-na.hosted.exlibrisgroup.com";
    private const string ReportFolder = "/shared/Universidad Continental 51UCCI_INST/Reports/API/";

    public async Task<AnalyticsReportPreview> InspectAsync(string report, CancellationToken cancellationToken)
    {
        ValidateReport(report);
        var query = $"path={Uri.EscapeDataString(ReportFolder + report)}&limit=25&col_names=true";
        var (xml, finished) = await RequestAsync(report, query, cancellationToken);
        var columns = ReadColumns(report, xml);
        return new AnalyticsReportPreview(report, columns,
            xml.Descendants().Count(e => e.Name.LocalName == "Row"), finished);
    }

    public async IAsyncEnumerable<AnalyticsReportPage> ReadPagesAsync(
        string report, DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateReport(report);
        var filter = CreateDateFilter(report, date);
        var query = $"path={Uri.EscapeDataString(ReportFolder + report)}&filter={Uri.EscapeDataString(filter)}&limit=1000&col_names=true";
        string? token = null;
        IReadOnlyList<AnalyticsColumn>? columns = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (xml, finished) = await RequestAsync(report, query, cancellationToken);
            var pageColumns = ReadColumns(report, xml, required: columns is null);
            if (columns is null) columns = pageColumns;
            else if (pageColumns.Count > 0 && !columns.SequenceEqual(pageColumns))
                throw new InvalidOperationException($"Alma/{report}: el esquema cambió durante la paginación.");

            var rows = ReadRows(report, xml, columns);
            if (!finished)
            {
                // Analytics devuelve el token en la primera respuesta; se reutiliza sin decodificarlo.
                token ??= xml.Elements().FirstOrDefault(e => e.Name.LocalName == "ResumptionToken")?.Value.Trim();
                if (string.IsNullOrEmpty(token))
                    throw new InvalidOperationException($"Alma/{report}: faltó el token para continuar.");
                if (rows.Count == 0)
                    throw new InvalidOperationException($"Alma/{report}: página vacía sin fin de reporte; extracción incompleta.");
                query = $"token={Uri.EscapeDataString(token)}&limit=1000&col_names=true";
            }

            yield return new AnalyticsReportPage(report, columns, rows, finished);
            if (finished) yield break;
        }
    }

    private static string CreateDateFilter(string report, DateOnly date)
    {
        XNamespace sawx = "com.siebel.analytics.web/expression/v1.1";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var field = report == "Loans" ? "\"Loan Date\".\"Loan Date\"" : "\"Renewal Date\".\"Renewal Date\"";
        return new XElement(sawx + "expr",
            new XAttribute(XNamespace.Xmlns + "sawx", sawx),
            new XAttribute(XNamespace.Xmlns + "saw", "com.siebel.analytics.web/report/v1.1"),
            new XAttribute(XNamespace.Xmlns + "xsi", xsi),
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XAttribute(xsi + "type", "sawx:comparison"), new XAttribute("op", "equal"),
            new XElement(sawx + "expr", new XAttribute(xsi + "type", "sawx:sqlExpression"), field),
            new XElement(sawx + "expr", new XAttribute(xsi + "type", "xsd:date"), date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static void ValidateReport(string report)
    {
        if (report is not ("Loans" or "Renewals"))
            throw new ArgumentException("Reporte válido: Loans o Renewals.", nameof(report));
    }

    private async Task<(XElement Result, bool Finished)> RequestAsync(
        string report, string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Falta configurar ALMA_API_KEY.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/almaws/v1/analytics/reports?{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("apikey", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Alma/{report}: HTTP {(int)response.StatusCode}. La extracción no se completó.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
        });
        var xml = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        if (xml.Descendants().Any(e => e.Name.LocalName == "error"))
            throw new InvalidOperationException($"Alma/{report}: la API devolvió un error. La extracción no se completó.");
        var result = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "QueryResult")
            ?? throw new InvalidOperationException($"Alma/{report}: respuesta sin QueryResult.");
        var finishedText = result.Elements().FirstOrDefault(e => e.Name.LocalName == "IsFinished")?.Value;
        if (!bool.TryParse(finishedText, out var finished))
            throw new InvalidOperationException($"Alma/{report}: respuesta sin IsFinished válido.");
        return (result, finished);
    }

    private static IReadOnlyList<AnalyticsColumn> ReadColumns(string report, XElement result, bool required = true)
    {
        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
        var columns = result.Descendants(xsd + "sequence").Elements(xsd + "element")
            .Select(e => new AnalyticsColumn(
                (string?)e.Attribute("name") ?? throw new InvalidOperationException("Columna sin identificador."),
                e.Attributes().FirstOrDefault(a => a.Name.LocalName == "columnHeading")?.Value
                    ?? (string?)e.Attribute("name") ?? "",
                (string?)e.Attribute("type") ?? "desconocido"))
            .ToArray();
        if (required && columns.Length == 0)
            throw new InvalidOperationException($"Alma/{report}: no se encontró el esquema de columnas.");
        if (columns.Select(c => c.Id).Distinct().Count() != columns.Length)
            throw new InvalidOperationException($"Alma/{report}: identificadores de columna duplicados.");
        return columns;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> ReadRows(
        string report, XElement result, IReadOnlyList<AnalyticsColumn> columns)
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var row in result.Descendants().Where(e => e.Name.LocalName == "Row"))
        {
            var values = columns.ToDictionary(c => c.Id, _ => (string?)null);
            var seen = new HashSet<string>();
            foreach (var cell in row.Elements())
            {
                var id = cell.Name.LocalName;
                if (!values.ContainsKey(id) || !seen.Add(id))
                    throw new InvalidOperationException($"Alma/{report}: una fila no coincide con el esquema.");
                values[id] = (string?)cell.Attribute(xsi + "nil") is "true" or "1" ? null : cell.Value;
            }
            rows.Add(values);
        }
        return rows;
    }
}
