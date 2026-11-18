using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Serilog;
using ILogger = Serilog.ILogger;

namespace workflowengine.paymentsprocessor.Extensions;

/// <summary>
/// Extension methods para integrar Azure Key Vault en Azure Functions.
/// WHY: Centraliza gestión de secretos en producción mientras permite desarrollo local con local.settings.json
/// </summary>
public static class KeyVaultConfigurationExtensions
{
    /// <summary>
    /// Crea el credential apropiado según la configuración disponible.
    /// WHY: ClientSecretCredential si hay credenciales explícitas (las 3),
    /// DefaultAzureCredential para Managed Identity (producción) o az login (desarrollo).
    /// </summary>
    /// <param name="configuration">Configuración con posibles valores de AzureAd.</param>
    /// <param name="logger">Logger de bootstrap para indicar qué credential se usa.</param>
    /// <returns>TokenCredential configurado para autenticar contra Azure Key Vault.</returns>
    internal static TokenCredential CreateCredential(IConfiguration configuration, ILogger logger)
    {
        return new DefaultAzureCredential();
    }

    /// <summary>
    /// Crea un logger temporal para bootstrap antes de la configuración completa de logging.
    /// WHY: La carga de Key Vault ocurre antes de que el pipeline de logging esté disponible.
    /// </summary>
    internal static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
