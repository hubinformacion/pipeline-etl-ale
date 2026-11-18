using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using workflowengine.paymentsprocessor.Constants;
using ILogger = Serilog.ILogger;

namespace workflowengine.paymentsprocessor.Extensions;

/// <summary>
/// Validación centralizada de configuración al inicio de la aplicación.
/// WHY: Permite detectar configuraciones faltantes antes de que fallen en runtime,
/// proporcionando feedback claro de qué está mal configurado.
/// </summary>
public static class ConfigurationValidationExtensions
{
    private static ILogger? _logger;

    /// <summary>
    /// Valida todas las configuraciones requeridas por la Azure Function.
    /// Loggea cada configuración faltante y lanza excepción al final si hay errores.
    /// </summary>
    public static void ValidateRequiredConfiguration(this IHostApplicationBuilder builder)
    {
        ValidateConfiguration(builder.Configuration);
    }

    /// <summary>
    /// Valida configuración directamente desde IConfiguration.
    /// WHY: La validación ocurre durante bootstrap, antes de que el pipeline de logging esté disponible,
    /// por lo que se usa un logger temporal de Serilog.
    /// </summary>
    public static void ValidateConfiguration(IConfiguration configuration)
    {
        _logger = CreateBootstrapLogger();
        var errors = new List<string>();

        _logger.Information("========================================");
        _logger.Information("VALIDANDO CONFIGURACION REQUERIDA");
        _logger.Information("========================================");

        // Validar Azure AD (opcional para desarrollo local, pero requerido para Key Vault)
        var azureAdTenantId = configuration["AzureAd:TenantId"];
        var azureAdClientId = configuration["AzureAd:ClientId"];
        var azureAdClientSecret = configuration["AzureAd:ClientSecret"];
        var vault = configuration["Vault"];

        var hasAzureAdConfig = !string.IsNullOrEmpty(azureAdTenantId) &&
                               !string.IsNullOrEmpty(azureAdClientId) &&
                               !string.IsNullOrEmpty(azureAdClientSecret) &&
                               !string.IsNullOrEmpty(vault);

        if (hasAzureAdConfig)
        {
            LogConfigStatus("AzureAd:TenantId", azureAdTenantId);
            LogConfigStatus("AzureAd:ClientId", azureAdClientId);
            LogConfigStatus("AzureAd:ClientSecret", azureAdClientSecret, isSecret: true);
            LogConfigStatus("Vault", vault);
        }
        else
        {
            _logger.Warning("[Azure AD/Key Vault] -> NO CONFIGURADO (modo desarrollo local)");
        }

        // Validar Timer Schedule (crítico - va en App Settings, no en Key Vault)
        ValidateConfigKey(configuration, "PaymentProcessingSchedule", errors);

        // Validar Workflow Engine API (crítico - compartido con Frontend)
        ValidateConfigKey(configuration, "WorkflowEngine:Api:BaseUrl", errors);
        ValidateConfigKey(configuration, "WorkflowEngine:Api:GetPendingPaymentsEndpoint", errors);
        ValidateConfigKey(configuration, "WorkflowEngine:Api:ConfirmPaymentEndpoint", errors);
        ValidateConfigKey(configuration, "WorkflowEngine:Api:RejectPaymentBySLAEndpoint", errors);

        // Validar APIM Subscription Key del motor de trámites (crítico - sin ella las llamadas al backend retornan 401)
        ValidateConfigKey(configuration, "WorkflowEngine:Security:ApimSubscriptionKey", errors, isSecret: true);

        // Payment API - MS de cuentas corrientes
        ValidateConfigKey(configuration, "WorkflowEngine:Integrations:PaymentApi:BaseUrl", errors);
        ValidateConfigKey(configuration, "WorkflowEngine:Integrations:PaymentApi:GetStatusDebtEndpoint", errors);
        ValidateConfigKey(configuration, "WorkflowEngine:Integrations:PaymentApi:ApimKey", errors, isSecret: true);

        // Resilience HTTP (crítico - consumido por AddStandardResilienceHandler al registrar los HttpClient)
        ValidateConfigKey(configuration, PaymentProcessingConstants.MaxRetriesConfigKey, errors);
        ValidateConfigKey(configuration, PaymentProcessingConstants.InitialDelayMsConfigKey, errors);

        // Paralelismo del procesamiento (crítico - controla el grado de concurrencia en Parallel.ForEachAsync)
        ValidateConfigKey(configuration, PaymentProcessingConstants.MaxDegreeOfParallelismConfigKey, errors);

        _logger.Information("========================================");

        if (errors.Count > 0)
        {
            _logger.Error("ERRORES DE CONFIGURACION DETECTADOS ({ErrorCount}):", errors.Count);
            foreach (var error in errors)
            {
                _logger.Error("  - {ConfigError}", error);
            }

            throw new InvalidOperationException(
                $"La aplicación no puede iniciar debido a {errors.Count} configuración(es) faltante(s). " +
                "Revise los errores anteriores y configure los valores requeridos en local.settings.json o Azure Key Vault.");
        }

        _logger.Information("Todas las configuraciones requeridas estan presentes");
    }

    /// <summary>
    /// Valida una clave de configuración específica y agrega error a la lista si está vacía.
    /// </summary>
    private static void ValidateConfigKey(IConfiguration configuration, string key, List<string> errors,
        bool isSecret = false)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            var errorMsg = $"{key} no está configurado";
            errors.Add(errorMsg);
            LogConfigStatus(key, null, isSecret);
        }
        else
        {
            LogConfigStatus(key, value, isSecret);
        }
    }

    /// <summary>
    /// Loggea el estado de una configuración usando el nivel apropiado según su estado.
    /// WHY: Diferencia visualmente entre configuraciones válidas, opcionales faltantes y errores.
    /// </summary>
    private static void LogConfigStatus(string key, string? value, bool isSecret = false, bool optional = false)
    {
        var status = string.IsNullOrWhiteSpace(value)
            ? (optional ? "NO CONFIGURADO (opcional)" : "VACIO/NULO")
            : isSecret
                ? $"OK (Longitud: {value.Length})"
                : "OK";

        if (string.IsNullOrWhiteSpace(value))
        {
            if (optional)
                _logger!.Warning("[{ConfigKey}] -> {Status}", key, status);
            else
                _logger!.Error("[{ConfigKey}] -> {Status}", key, status);
        }
        else
        {
            _logger!.Information("[{ConfigKey}] -> {Status}", key, status);
        }
    }

    /// <summary>
    /// Crea un logger temporal para bootstrap antes de la configuración completa de logging.
    /// WHY: La validación de configuración ocurre antes de que el pipeline de logging esté disponible.
    /// </summary>
    private static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
