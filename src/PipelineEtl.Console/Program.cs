using System.Globalization;
using PipelineEtl.Application;
using PipelineEtl.Infrastructure.Sources.Alma;

if (args is ["--help"])
{
    Console.WriteLine("Uso: PipelineEtl.Console [--date YYYY-MM-DD] [--source alma|libcal|elogim|all]");
    Console.WriteLine("Extracción completa sin guardar: --extract-alma [--date YYYY-MM-DD] (consulta Loans y Renewals para D−2).");
    Console.WriteLine("Inspección de columnas: --inspect-alma (requiere ALMA_API_KEY; sin filtro de fecha ni extracción completa).");
    Console.WriteLine("Sin fecha: hoy en America/Lima. Sin fuente: all. Solo calcula fechas; no extrae datos.");
    return 0;
}

try
{
    if (args is ["--inspect-alma"])
    {
        var apiKey = Environment.GetEnvironmentVariable("ALMA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Configura ALMA_API_KEY en la terminal; no la escribas en el código.");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var client = new AlmaAnalyticsClient(httpClient, apiKey);
        Console.WriteLine("Inspección sin filtro de fecha: primera página, hasta 25 filas por reporte. No guarda datos.");
        foreach (var report in new[] { "Loans", "Renewals" })
        {
            var preview = await client.InspectAsync(report, cancellation.Token);
            Console.WriteLine($"{preview.Report}: {preview.Columns.Count} columnas; {preview.SampleRowCount} filas en la muestra; fin del reporte: {preview.IsFinished}.");
            foreach (var column in preview.Columns)
                Console.WriteLine($"  {column.Id} | {column.Heading} | {column.Type}");
        }
        return 0;
    }

    var extractAlma = args.Contains("--extract-alma");
    if (args.Count(a => a == "--extract-alma") > 1)
        throw new ArgumentException("No repitas --extract-alma.");
    if (extractAlma) args = args.Where(a => a != "--extract-alma").ToArray();

    DateOnly? requestedDate = null;
    string source = "all";
    var seen = new HashSet<string>();
    for (int i = 0; i < args.Length; i += 2)
    {
        var option = args[i];
        if (option is not ("--date" or "--source") || !seen.Add(option) || i + 1 >= args.Length)
            throw new ArgumentException("Argumentos desconocidos, duplicados o incompletos. Usa --help.");

        var value = args[i + 1];
        if (option == "--source")
            source = value;
        else if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out var parsed))
            requestedDate = parsed;
        else
            throw new ArgumentException("Fecha inválida. Usa --date YYYY-MM-DD con una fecha existente.");
    }

    var referenceDate = requestedDate ?? ExtractionPlanner.Today(DateTimeOffset.UtcNow);
    if (extractAlma && seen.Contains("--source") && source != "alma")
        throw new ArgumentException("--extract-alma solo admite --source alma.");
    var plan = ExtractionPlanner.Create(referenceDate, extractAlma ? "alma" : source);
    Console.WriteLine($"Fecha de referencia: {referenceDate:yyyy-MM-dd} ({ExtractionPlanner.TimeZoneId})");
    foreach (var day in plan)
        Console.WriteLine($"{day.Source}: {day.Date:yyyy-MM-dd}");
    if (extractAlma)
    {
        var apiKey = Environment.GetEnvironmentVariable("ALMA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Configura ALMA_API_KEY en la terminal; no la escribas en el código.");
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var extraction = new ExtractAlmaReports(new AlmaAnalyticsClient(httpClient, apiKey));
        Console.WriteLine("Extracción de todas las columnas por páginas. No se guardarán datos.");
        await foreach (var progress in extraction.RunAsync(plan.Single().Date, cancellation.Token))
        {
            Console.WriteLine($"{progress.Report}: {progress.ColumnCount} columnas; páginas: {progress.PageCount}; filas acumuladas: {progress.RowCount}; completo: {progress.IsFinished}.");
        }
        Console.WriteLine("Extracción de Loans y Renewals completada y fechas verificadas. Sin persistencia.");
    }
    else
        Console.WriteLine("Solo planificación: no se consultaron fuentes ni se escribieron datos.");
    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

catch (OperationCanceledException)
{
    Console.Error.WriteLine("Consulta cancelada o tiempo de espera agotado. No se guardaron datos.");
    return 1;
}
catch (HttpRequestException)
{
    Console.Error.WriteLine("No se pudo conectar con Alma. Revisa red y disponibilidad del servicio.");
    return 1;
}
catch (System.Xml.XmlException)
{
    Console.Error.WriteLine("Alma devolvió un XML inválido o no admitido.");
    return 1;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
