namespace PipelineEtl.Domain;

// Los valores originales se mantienen como texto; null y cadena vacía son distintos.
public sealed record AnalyticsReportPage(
    string Report,
    IReadOnlyList<AnalyticsColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    bool IsFinished);

public sealed record AlmaExtractionProgress(
    string Report, DateOnly Date, int ColumnCount, int PageCount, long RowCount, bool IsFinished);
