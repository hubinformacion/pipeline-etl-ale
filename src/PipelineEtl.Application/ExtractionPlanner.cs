using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public static class ExtractionPlanner
{
    public const string TimeZoneId = "America/Lima";

    public static DateOnly Today(DateTimeOffset instant) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);

    public static IReadOnlyList<ExtractionDay> Create(DateOnly referenceDate, string source)
    {
        if (source is not ("all" or "alma" or "libcal" or "elogim"))
            throw new ArgumentException("Fuente válida: alma, libcal, elogim o all.");

        var sources = new[] { (Name: "alma", Offset: -2), (Name: "libcal", Offset: 0), (Name: "elogim", Offset: -1) };
        return sources.Where(s => source == "all" || s.Name == source)
            .Select(s => new ExtractionDay(s.Name, referenceDate.AddDays(s.Offset))).ToArray();
    }
}
