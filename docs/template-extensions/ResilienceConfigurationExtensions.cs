namespace workflowengine.paymentsprocessor.Extensions;

public static class ResilienceConfigurationExtensions
{
    /// <summary>
    /// Configura las opciones de resilience para los HttpClient del processor.
    /// Replica el perfil del backend (reintentos con backoff exponencial + jitter, timeout por intento
    /// y circuit breaker) para que ambos servicios reaccionen igual ante fallas transitorias de APIs externas.
    /// </summary>
    /// <param name="options">Opciones estándar del pipeline de resilience a configurar</param>
    /// <param name="maxRetries">Número máximo de reintentos por request fallido</param>
    /// <param name="initialDelayMs">Delay inicial en milisegundos antes del primer reintento</param>
    public static void ConfigureStandardResilience(HttpStandardResilienceOptions options, int maxRetries,
        int initialDelayMs)
    {
        options.Retry.MaxRetryAttempts = maxRetries;
        options.Retry.Delay = TimeSpan.FromMilliseconds(initialDelayMs);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(PaymentProcessingConstants.DefaultAttemptTimeoutSeconds);

        options.CircuitBreaker.SamplingDuration =
            TimeSpan.FromSeconds(PaymentProcessingConstants.DefaultCircuitBreakerSamplingSeconds);
        options.CircuitBreaker.FailureRatio = PaymentProcessingConstants.DefaultCircuitBreakerFailureRatio;
        options.CircuitBreaker.MinimumThroughput = PaymentProcessingConstants.DefaultCircuitBreakerMinimumThroughput;
        options.CircuitBreaker.BreakDuration =
            TimeSpan.FromSeconds(PaymentProcessingConstants.DefaultCircuitBreakerBreakSeconds);
    }
}
