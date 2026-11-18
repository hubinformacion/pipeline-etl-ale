namespace workflowengine.paymentsprocessor.Extensions;

public static class UrlNormalizer
{
    /// <summary>
    /// Asegura trailing "/" en BaseAddress para que HttpClient resuelva URIs relativos
    /// sin descartar segmentos del path (ej. "/security" en URLs de APIM).
    /// </summary>
    public static Uri NormalizeBaseUrl(string baseUrl) => new(baseUrl.TrimEnd('/') + "/");

    /// <summary>
    /// Remueve "/" inicial de endpoints para que HttpClient los trate como relativos al BaseAddress
    /// en lugar de absolutos desde la raíz del host (RFC 3986).
    /// </summary>
    public static string NormalizeEndpoint(string endpoint) => endpoint.TrimStart('/');
}
