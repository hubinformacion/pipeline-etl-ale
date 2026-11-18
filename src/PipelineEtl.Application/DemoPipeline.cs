using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public sealed class DemoPipeline
{
    public RunSummary Run() => new("Base ETL ejecutada. Aún no se consultan fuentes ni se escriben datos.");
}
