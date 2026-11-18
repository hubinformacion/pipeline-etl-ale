namespace PipelineEtl.Domain;

public sealed record ElogimColumn(string Name, string DatabaseType, string DotNetType, bool? IsNullable);
