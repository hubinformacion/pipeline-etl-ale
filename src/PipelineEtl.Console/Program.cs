using System.Globalization;
using System.Text.Json;
using PipelineEtl.Domain;
using PipelineEtl.Application;
using PipelineEtl.Infrastructure.Sources.Alma;
using PipelineEtl.Infrastructure.Sources.Elogim;
using PipelineEtl.Infrastructure.Sources.LibCal;
using PipelineEtl.Infrastructure.Persistence.PostgreSql;

if (args is ["--help"])
{
    Console.WriteLine("Uso: PipelineEtl.Console [--date YYYY-MM-DD] [--source alma|libcal|elogim|all]");
    Console.WriteLine("Extracción completa sin guardar: --extract-alma [--date YYYY-MM-DD] (consulta Loans y Renewals para D−2).");
    Console.WriteLine("Inicializar tablas: --init-db (requiere NEON_CONNECTION_STRING en formato .NET).");
    Console.WriteLine("Cargar en PostgreSQL: --load-alma [--date YYYY-MM-DD] (reemplaza D−2 por reporte, en transacción).");
    Console.WriteLine("Inspección de columnas: --inspect-alma (requiere ALMA_API_KEY; sin filtro de fecha ni extracción completa).");
    Console.WriteLine("Extracción Elogim sin guardar: --extract-elogim [--date YYYY-MM-DD] (consulta D−1).");
    Console.WriteLine("Cargar Elogim en PostgreSQL: --load-elogim [--date YYYY-MM-DD] (reemplaza D−1 en transacción).");
    Console.WriteLine("Inspección Elogim: --inspect-elogim (requiere ELOGIM_CONNECTION_STRING; solo nombres y tipos).");
    Console.WriteLine("LibCal: --inspect-libcal o --extract-libcal [--date YYYY-MM-DD] (D, reservas de uso; sin persistencia).");
    Console.WriteLine("Cargar LibCal: --load-libcal [--date YYYY-MM-DD] (foto completa D; solo admite hoy en Perú).");
    Console.WriteLine("Sin fecha: hoy en America/Lima. Sin fuente: all. Solo calcula fechas; no extrae datos.");
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

try
{
    if (args is ["--init-db"])
    {
        await using var database = new PostgresDatabase(GetPostgresConnectionString());
        await database.InitializeAsync(cancellation.Token);
        Console.WriteLine("Esquema etl y tablas Alma/Elogim/LibCal preparados. No se cargaron datos.");
        return 0;
    }

    if (args is ["--inspect-elogim"])
    {
        var client = CreateElogimClient();
        var columns = await client.InspectAsync(cancellation.Token);
        Console.WriteLine($"Elogim: {columns.Count} columnas. Solo esquema; sin lectura de filas ni escritura.");
        foreach (var column in columns)
            Console.WriteLine($"  {column.Name} | {column.DatabaseType} | {column.DotNetType} | nullable: {column.IsNullable}");
        return 0;
    }

    if (args is ["--inspect-alma"])
    {
        var apiKey = Environment.GetEnvironmentVariable("ALMA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Configura ALMA_API_KEY en la terminal; no la escribas en el código.");

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

    var inspectLibCal = args.Contains("--inspect-libcal");
    var extractLibCal = args.Contains("--extract-libcal");
    var loadLibCal = args.Contains("--load-libcal");
    var extractElogim = args.Contains("--extract-elogim");
    var loadElogim = args.Contains("--load-elogim");
    var extractAlma = args.Contains("--extract-alma");
    var loadAlma = args.Contains("--load-alma");
    if (args.Count(a => a is "--extract-alma" or "--load-alma" or "--extract-elogim" or "--load-elogim" or "--inspect-libcal" or "--extract-libcal" or "--load-libcal") > 1)
        throw new ArgumentException("Elige solo un modo de extracción o carga, sin repetirlo.");
    args = args.Where(a => a is not ("--extract-alma" or "--load-alma" or "--extract-elogim" or "--load-elogim" or "--inspect-libcal" or "--extract-libcal" or "--load-libcal")).ToArray();

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
    if ((extractAlma || loadAlma) && seen.Contains("--source") && source != "alma")
        throw new ArgumentException("La extracción/carga de Alma solo admite --source alma.");
    if ((extractElogim || loadElogim) && seen.Contains("--source") && source != "elogim")
        throw new ArgumentException("La extracción de Elogim solo admite --source elogim.");
    if ((inspectLibCal || extractLibCal || loadLibCal) && seen.Contains("--source") && source != "libcal")
        throw new ArgumentException("La inspección/extracción/carga de LibCal solo admite --source libcal.");
    var plan = ExtractionPlanner.Create(referenceDate, extractAlma || loadAlma ? "alma" : extractElogim || loadElogim ? "elogim" : inspectLibCal || extractLibCal || loadLibCal ? "libcal" : source);
    Console.WriteLine($"Fecha de referencia: {referenceDate:yyyy-MM-dd} ({ExtractionPlanner.TimeZoneId})");
    foreach (var day in plan)
        Console.WriteLine($"{day.Source}: {day.Date:yyyy-MM-dd}");
    if (inspectLibCal || extractLibCal || loadLibCal)
    {
        var clientId = Environment.GetEnvironmentVariable("LIBCAL_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("LIBCAL_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Configura LIBCAL_CLIENT_ID y LIBCAL_CLIENT_SECRET en la terminal; no compartas los valores en el chat ni en Git.");
        var categories = LoadLibCalCategories();
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(120) };
        var extraction = new ExtractLibCalBookings(new LibCalClient(httpClient, clientId, clientSecret), categories);
        if (inspectLibCal)
        {
            Console.WriteLine("LibCal: primera página por categoría (hasta 25 filas); solo conteos y nombres/tipos de campos. Sin persistencia.");
            var allFields = new Dictionary<string, HashSet<string>>();
            await foreach (var sample in extraction.InspectAsync(plan.Single().Date, cancellation.Token))
            {
                Console.WriteLine($"{sample.Category.LocationName} / {sample.Category.Name} (cid {sample.Category.Id}): muestra {sample.SampleRowCount}; puede haber más: {sample.HasMore}.");
                var questions = sample.Fields.Where(f => f.Name.Length > 1 && f.Name[0] == 'q' &&
                    f.Name.Skip(1).All(char.IsAsciiDigit));
                foreach (var question in questions)
                    Console.WriteLine($"  {question.Name}: {question.NonBlankCount}/{sample.SampleRowCount} con respuesta.");
                foreach (var field in sample.Fields)
                {
                    if (!allFields.TryGetValue(field.Name, out var types)) allFields[field.Name] = types = [];
                    types.UnionWith(field.Types);
                }
            }
            Console.WriteLine("Campos observados (no incluye valores):");
            foreach (var field in allFields.OrderBy(f => f.Key))
                Console.WriteLine($"  {field.Key} | {string.Join(", ", field.Value.Order())}");
        }
        else if (loadLibCal)
        {
            await using var store = new PostgresLibCalBookingStore(GetPostgresConnectionString());
            Console.WriteLine("LibCal: reemplazando foto D de todas las categorías en una transacción; incluye canceladas, tentativas y denegadas.");
            var count = await extraction.LoadAsync(plan.Single().Date, store, cancellation.Token);
            Console.WriteLine($"LibCal: carga confirmada; {count} filas; {categories.Count} categorías. Reporte disponible en etl.libcal_bookings.");
        }
        else
        {
            Console.WriteLine("LibCal: extracción paginada por categoría; incluye canceladas, tentativas y denegadas. Sin persistencia.");
            long total = 0;
            await foreach (var progress in extraction.RunAsync(plan.Single().Date, cancellation.Token))
            {
                if (!progress.IsFinished) continue;
                total += progress.RowCount;
                Console.WriteLine($"{progress.Category.LocationName} / {progress.Category.Name} (cid {progress.Category.Id}): {progress.RowCount} filas; {progress.PageCount} páginas.");
            }
            Console.WriteLine($"LibCal: extracción completada; {total} filas; {categories.Count} categorías. Sin persistencia.");
        }
    }
    else if (extractElogim || loadElogim)
    {
        var date = plan.Single().Date;
        var end = date.AddDays(1);
        Console.WriteLine($"Elogim: intervalo [{date:yyyy-MM-dd} 00:00:00, {end:yyyy-MM-dd} 00:00:00). Hora local Perú.");
        var extraction = new ExtractElogim(CreateElogimClient());
        if (loadElogim)
        {
            await using var store = new PostgresElogimSessionStore(GetPostgresConnectionString());
            Console.WriteLine("Cargando Elogim. Repetir el día reemplaza sus filas; solo se confirma tras leer la consulta completa.");
            var count = await extraction.LoadAsync(date, store, cancellation.Token);
            Console.WriteLine($"Elogim: carga confirmada; {count} filas; 5 columnas.");
        }
        else
        {
            var count = await extraction.RunAsync(date, cancellation.Token);
            Console.WriteLine($"Elogim: extracción completada; {count} filas; 5 columnas. Sin persistencia.");
        }
    }
    else if (extractAlma || loadAlma)
    {
        var apiKey = Environment.GetEnvironmentVariable("ALMA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Configura ALMA_API_KEY en la terminal; no la escribas en el código.");
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var extraction = new ExtractAlmaReports(new AlmaAnalyticsClient(httpClient, apiKey));
        if (loadAlma)
        {
            await using var store = new PostgresAlmaReportStore(GetPostgresConnectionString());
            Console.WriteLine("Cargando Loans y Renewals. Cada reporte/día se confirma por separado; repetir el día reemplaza sus filas.");
            await foreach (var progress in extraction.LoadAsync(plan.Single().Date, store, cancellation.Token))
                Console.WriteLine($"{progress.Report}: carga confirmada; {progress.RowCount} filas; {progress.PageCount} páginas; {progress.ColumnCount} columnas.");
            Console.WriteLine("Carga de ambos reportes completada.");
        }
        else
        {
            Console.WriteLine("Extracción de todas las columnas por páginas. No se guardarán datos.");
            await foreach (var progress in extraction.RunAsync(plan.Single().Date, cancellation.Token))
                Console.WriteLine($"{progress.Report}: {progress.ColumnCount} columnas; páginas: {progress.PageCount}; filas acumuladas: {progress.RowCount}; completo: {progress.IsFinished}.");
            Console.WriteLine("Extracción de Loans y Renewals completada y fechas verificadas. Sin persistencia.");
        }
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
    Console.Error.WriteLine("Consulta cancelada o tiempo de espera agotado. Los reportes ya confirmados se conservan; puedes repetir el día completo.");
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

static string GetPostgresConnectionString()
{
    var connectionString = Environment.GetEnvironmentVariable("NEON_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new ArgumentException("Configura NEON_CONNECTION_STRING en la terminal; no compartas la cadena en el chat ni en Git.");
    return connectionString;
}

static ElogimClient CreateElogimClient()
{
    var connectionString = Environment.GetEnvironmentVariable("ELOGIM_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new ArgumentException("Configura ELOGIM_CONNECTION_STRING en la terminal; no compartas las credenciales en el chat ni en Git.");
    return new ElogimClient(connectionString, Environment.GetEnvironmentVariable("ELOGIM_TABLE") ?? "est_ezproxy");
}

static IReadOnlyList<LibCalCategory> LoadLibCalCategories()
{
    try
    {
        var categories = JsonSerializer.Deserialize<List<LibCalCategory>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "libcal.categories.json")));
        if (categories is null || categories.Count == 0 || categories.Any(c => c is null ||
                string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.LocationId) ||
                string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.LocationName)))
            throw new InvalidOperationException("LibCal: configuración de categorías incompleta.");
        return categories;
    }
    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
    {
        throw new InvalidOperationException("No se pudo leer libcal.categories.json; verifica el archivo de configuración.");
    }
}
