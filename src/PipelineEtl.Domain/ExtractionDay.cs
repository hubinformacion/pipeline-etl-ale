namespace PipelineEtl.Domain;

public sealed record ExtractionDay(string Source, DateOnly Date);
