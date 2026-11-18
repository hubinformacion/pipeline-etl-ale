using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PipelineEtl.Application;

namespace PipelineEtl.Functions.Functions;

public sealed class DemoFunction(DemoPipeline pipeline, ILogger<DemoFunction> logger)
{
    [Function("DemoPipeline")]
    public void Run([TimerTrigger("%DemoSchedule%")] TimerInfo timer)
    {
        logger.LogInformation("{Message}", pipeline.Run().Message);
    }
}
