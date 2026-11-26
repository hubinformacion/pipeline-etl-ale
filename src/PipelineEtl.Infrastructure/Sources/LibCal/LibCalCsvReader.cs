using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using PipelineEtl.Application;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Sources.LibCal;

public static class LibCalCsvReader
{
    private const string FormHeader = "Booking Form Answers";
    private static readonly string[] TimestampFormats = ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];

    public static IEnumerable<(LibCalCategory Category, LibCalBooking Booking)> Read(
        string path, IReadOnlyList<LibCalCategory> categories, CancellationToken cancellationToken)
    {
        using var parser = new TextFieldParser(path, Encoding.UTF8, true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        var header = ReadFields(parser);
        var formStart = Array.FindIndex(header, name => name.Trim() == FormHeader);
        if (formStart != header.Length - 1 || formStart < 0)
            throw new InvalidOperationException($"LibCal CSV: se esperaba '{FormHeader}' al final del encabezado.");

        int Column(string name)
        {
            var indexes = Enumerable.Range(0, formStart).Where(i => header[i].Trim() == name).ToArray();
            return indexes.Length == 1
                ? indexes[0]
                : throw new InvalidOperationException($"LibCal CSV: se esperaba exactamente una columna '{name}'.");
        }

        var bookingId = Column("ID de reserva");
        var space = Column("Nombre del espacio/asiento");
        var location = Column("Ubicación");
        var categoryName = Column("Categoría");
        var firstName = Column("Nombre");
        var lastName = Column("Apellido");
        var email = Column("Correo electrónico");
        var fromDate = Column("Desde la fecha");
        var fromTime = Column("De vez en cuando");
        var toDate = Column("Hasta la fecha");
        var toTime = Column("Al tiempo");
        var status = Column("Estado");
        var attendance = Column("Apareció");
        var lima = TimeZoneInfo.FindSystemTimeZoneById(ExtractionPlanner.TimeZoneId);

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = parser.LineNumber;
            var fields = ReadFields(parser);
            if (fields.Length < formStart)
                throw InvalidRow(line, "faltan columnas de la reserva");

            var campusName = fields[location].Trim();
            var exportedCategory = fields[categoryName].Trim();
            // En Cusco e Instituto el export usa el nombre original de la categoría.
            if (exportedCategory.Equals("Computadoras", StringComparison.OrdinalIgnoreCase) &&
                (campusName.Equals("Campus Cusco", StringComparison.OrdinalIgnoreCase) ||
                 campusName.Equals("Campus Instituto", StringComparison.OrdinalIgnoreCase)))
                exportedCategory = "Computadoras y laptops";

            var matches = categories.Where(c =>
                string.Equals(c.LocationName, campusName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Name, exportedCategory, StringComparison.OrdinalIgnoreCase));
            var category = matches.SingleOrDefault()
                ?? throw InvalidRow(line, "el campus o la categoría no figuran en el catálogo");

            var startsAt = ReadTimestamp(fields[fromDate] + " " + fields[fromTime], line);
            var endsAt = ReadTimestamp(fields[toDate] + " " + fields[toTime], line);
            if (endsAt <= startsAt)
                throw InvalidRow(line, "el fin debe ser posterior al inicio");

            var answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var i = formStart;
            while (i < fields.Length && !IsNote(fields[i].Trim()))
            {
                var label = fields[i].Trim();
                if (i + 1 >= fields.Length || label.Length == 0 || !answers.TryAdd(label, Value(fields[i + 1])))
                    throw InvalidRow(line, "el formulario debe contener pares de pregunta y respuesta");
                i += 2;
            }

            DateTime? latestAt = null;
            string? latestNote = null;
            // El export añade cada nota como etiqueta, fecha, autor y texto.
            for (; i < fields.Length; i += 4)
            {
                if (i + 3 >= fields.Length || !IsNote(fields[i].Trim()))
                    throw InvalidRow(line, "la nota debe contener etiqueta, fecha, autor y texto");
                var created = ReadTimestamp(fields[i + 1], line);
                var note = Value(fields[i + 3]);
                if (note is not null && (latestAt is null || created >= latestAt))
                {
                    latestAt = created;
                    latestNote = note;
                }
            }

            var groupSpace = category.Name == "Espacios grupales";
            var bookingStatus = Value(fields[status]);
            if (bookingStatus?.StartsWith("Cancelado", StringComparison.OrdinalIgnoreCase) == true)
                bookingStatus = "Cancelado";
            var confirmation = Value(fields[attendance])?.ToLowerInvariant() switch
            {
                "sí" or "si" => "Asistió",
                "no" => "No asistió",
                null => null,
                _ => throw InvalidRow(line, "el valor de asistencia no se reconoce")
            };

            yield return (category, new LibCalBooking(
                null,
                Value(fields[bookingId]) ?? throw InvalidRow(line, "falta el código de reserva"),
                new DateTimeOffset(startsAt, lima.GetUtcOffset(startsAt)),
                new DateTimeOffset(endsAt, lima.GetUtcOffset(endsAt)),
                Value(fields[email]), Value(fields[firstName]), Value(fields[lastName]),
                Value(fields[space]) ?? throw InvalidRow(line, "falta el recurso"),
                bookingStatus, confirmation,
                answers.GetValueOrDefault("Número de celular"),
                groupSpace ? answers.GetValueOrDefault("Correo del integrante 2") : null,
                groupSpace ? answers.GetValueOrDefault("Correo del integrante 3") : null,
                latestNote));
        }
    }

    private static string[] ReadFields(TextFieldParser parser)
    {
        try
        {
            return parser.ReadFields() ?? [];
        }
        catch (MalformedLineException)
        {
            throw InvalidRow(parser.ErrorLineNumber, "hay comillas o delimitadores inválidos");
        }
    }

    private static DateTime ReadTimestamp(string value, long line)
    {
        if (DateTime.TryParseExact(value.Trim(), TimestampFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            return timestamp;
        throw InvalidRow(line, "hay una fecha u hora inválida");
    }

    private static bool IsNote(string label) =>
        label.StartsWith("Nota ", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(label.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0;

    private static string? Value(string field) =>
        string.IsNullOrWhiteSpace(field) ? null : field.Trim();

    private static InvalidOperationException InvalidRow(long line, string reason) =>
        new($"LibCal CSV, línea {line}: {reason}.");
}
