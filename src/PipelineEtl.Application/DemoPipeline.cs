using PipelineEtl.Domain;

namespace PipelineEtl.Application;

public sealed class DemoPipeline
{
    public RunSummary Run() => new("Mi primera ejecución del pipeline ETL.");
}
