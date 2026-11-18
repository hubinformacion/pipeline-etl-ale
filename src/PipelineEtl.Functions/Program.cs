using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PipelineEtl.Application;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => services.AddSingleton<DemoPipeline>())
    .Build();
host.Run();
