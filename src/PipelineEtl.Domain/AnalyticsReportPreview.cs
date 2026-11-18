namespace PipelineEtl.Domain;

public sealed record AnalyticsColumn(string Id, string Heading, string Type);

public sealed record AnalyticsReportPreview(
    string Report, IReadOnlyList<AnalyticsColumn> Columns, int SampleRowCount, bool IsFinished);
