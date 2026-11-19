using System.Globalization;
using NpgsqlTypes;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Persistence.PostgreSql;

internal sealed record AlmaTableColumn(string Name, string Heading, string XmlType, NpgsqlDbType DbType);

internal sealed record AlmaTableMapping(string Table, IReadOnlyList<AlmaTableColumn> Columns)
{
    private const string UserName = "CASE WHEN In House Loan Indicator = 'Y' THEN 'Uso interno' ELSE CASE WHEN First Name IS NULL OR First Name = '' THEN UPPER(Last Name) ELSE UPPER(Last Name || ', ' || First Name) END END";
    private const string Material = "CASE WHEN Material Type = 'BOOK' THEN 'Libro' WHEN Material Type = 'ISSUE' THEN 'Revista' WHEN Material Type = 'DVD' THEN 'DVD' WHEN Material Type = 'CDROM' THEN 'CD-ROM' WHEN Material Type = 'MAP' THEN 'Mapa' WHEN Material Type = 'THESIS' THEN 'Tesis' WHEN Material Type = 'PAMPHLET' THEN 'Folleto' WHEN Material Type = 'CASE' THEN 'Expediente' WHEN Material Type = 'SPECIALTHESIS' THEN 'Tesis de grado especial' WHEN Material Type = 'MASTERTHESIS' THEN 'Tesis de posgrado' WHEN Material Type = 'BACHLORTHESIS' THEN 'Tesis de licenciatura' WHEN Material Type = 'VRECORD' THEN 'Videograbación' WHEN Material Type = 'ARTICLE' THEN 'Artículo' WHEN Material Type = 'ATLAS' THEN 'Atlas' WHEN Material Type = 'NEWSPAPER' THEN 'Periódico' WHEN Material Type = 'ADDITIONAL1' THEN 'Obra literaria' WHEN Material Type = 'ADDITIONAL2' THEN 'Informe profesional' WHEN Material Type = 'ADDITIONAL3' THEN 'Test psicológico' WHEN Material Type = 'ADDITIONAL4' THEN 'Proyecto de investigación' WHEN Material Type = 'ADDITIONAL5' THEN 'Obra de referencia' WHEN Material Type = 'ADDITIONAL9' THEN 'Norma Técnica' ELSE Material Type END";

    private static AlmaTableColumn Text(string name, string heading) => new(name, heading, "xsd:string", NpgsqlDbType.Text);
    private static AlmaTableColumn Date(string name, string heading) => new(name, heading, "xsd:date", NpgsqlDbType.Date);

    public static AlmaTableMapping For(string report) => report switch
    {
        "Loans" => new("etl.alma_loans", [
            Text("phone_number", "CASE WHEN In House Loan Indicator = 'Y' THEN 'Uso interno' ELSE Preferred Phone Number END"),
            Text("campus_name", "Campus Name"), Text("circ_desk_name", "Circ Desk Name"),
            Date("loan_date", "Loan Date"), Text("author", "Author"), Text("barcode", "Barcode"),
            Text("in_house_loan_indicator", "In House Loan Indicator"), Text("item_id", "Item Id"),
            Text("item_loan_id", "Item Loan Id"), Text("loan_status", "Loan Status"), Text("loan_time", "Loan Time"),
            Text("mms_id", "MMS Id"), Text("title", "Title"), Text("item_policy", "Item Policy"),
            Text("user_name", UserName),
            Text("user_primary_identifier", "CASE WHEN In House Loan Indicator = 'Y' THEN 'Uso interno' ELSE LOWER(User Primary Identifier) END"),
            Text("preferred_email", "CASE WHEN In House Loan Indicator = 'Y' THEN 'Uso interno' ELSE LOWER(Preferred Email) END"),
            Text("material_type", Material)]),
        "Renewals" => new("etl.alma_renewals", [
            Text("campus_name", "Campus Name"), Text("author", "Author"), Text("barcode", "Barcode"),
            Text("item_id", "Item Id"), Text("item_loan_id", "Item Loan Id"), Text("loan_status", "Loan Status"),
            Text("mms_id", "MMS Id"), Text("title", "Title"), Text("item_policy", "Item Policy"),
            Text("phone_number", "Preferred Phone Number"), Text("renewal_campus_name", "Renewal Campus Name"),
            Date("renewal_date", "Renewal Date"), Text("user_name", UserName), Text("material_type", Material),
            Text("user_primary_identifier", "LOWER(User Primary Identifier)"), Text("preferred_email", "LOWER(Preferred Email)"),
            new("renewals", "Renewals", "xsd:double", NpgsqlDbType.Double)]),
        _ => throw new ArgumentException("Reporte válido: Loans o Renewals.")
    };

    public void ValidateSchema(IReadOnlyList<AnalyticsColumn> actual)
    {
        if (actual.Count(c => c.Id != "Column0") != Columns.Count)
            throw new InvalidOperationException($"{Table}: cambió el número de columnas de Analytics; actualiza el mapeo antes de cargar.");
        var byId = actual.ToDictionary(c => c.Id);
        for (int i = 0; i < Columns.Count; i++)
        {
            var expected = Columns[i];
            if (!byId.TryGetValue(SourceColumnId(i), out var column) || column.Type != expected.XmlType ||
                Normalize(column.Heading) != Normalize(expected.Heading))
                throw new InvalidOperationException($"{Table}: cambió {SourceColumnId(i)}; se canceló la carga para evitar asignaciones incorrectas.");
        }
    }

    // Analytics reserva Column0 como auxiliar; las columnas del reporte empiezan en Column1.
    public static string SourceColumnId(int index) => $"Column{index + 1}";

    public object? ConvertValue(int index, string? value)
    {
        if (value is null) return null;
        var column = Columns[index];
        if (column.DbType == NpgsqlDbType.Text) return value;
        if (column.DbType == NpgsqlDbType.Date && DateOnly.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        if (column.DbType == NpgsqlDbType.Double && double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) return number;
        throw new InvalidOperationException($"{Table}: valor no válido para {column.Name}; se canceló la carga.");
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
