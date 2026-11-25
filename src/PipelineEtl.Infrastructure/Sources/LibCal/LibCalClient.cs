using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PipelineEtl.Application;
using PipelineEtl.Application.Abstractions;
using PipelineEtl.Domain;

namespace PipelineEtl.Infrastructure.Sources.LibCal;

public sealed class LibCalClient(
    HttpClient httpClient, string clientId, string clientSecret, TimeProvider? timeProvider = null) : ILibCalClient
{
    private const string BaseUrl = "https://continental.libcal.com/1.1";
    private const int PageSize = 100;
    private const int MaxPages = 10000;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private string? accessToken;
    private DateTimeOffset expiresAt;

    public async Task<LibCalInspection> InspectAsync(
        LibCalCategory category, DateOnly date, CancellationToken cancellationToken)
    {
        ValidateRequest(category, date);
        using var document = await GetPageAsync(category, date, 1, 25, cancellationToken);
        ValidateRequest(category, date);
        var fields = new Dictionary<string, HashSet<string>>();
        var nonBlankCounts = new Dictionary<string, int>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            RequireObject(row);
            foreach (var property in row.EnumerateObject())
            {
                if (!fields.TryGetValue(property.Name, out var types))
                    fields[property.Name] = types = [];
                types.Add(property.Value.ValueKind.ToString());
                if ((property.Name == "internal_notes" && ReadLatestInternalNote(row) is not null) ||
                    property.Value.ValueKind == JsonValueKind.Number ||
                    (property.Value.ValueKind == JsonValueKind.String &&
                     !string.IsNullOrWhiteSpace(property.Value.GetString())))
                    nonBlankCounts[property.Name] = nonBlankCounts.GetValueOrDefault(property.Name) + 1;
            }
        }
        var count = document.RootElement.GetArrayLength();
        return new LibCalInspection(category, count, count == 25,
            fields.OrderBy(f => f.Key).Select(f => new LibCalField(
                f.Key, f.Value.Order().ToArray(), nonBlankCounts.GetValueOrDefault(f.Key))).ToArray());
    }

    public async IAsyncEnumerable<LibCalBookingPage> ReadPagesAsync(
        LibCalCategory category, DateOnly date, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateRequest(category, date);
        var seenPages = new HashSet<string>();
        for (int page = 1; page <= MaxPages; page++)
        {
            using var document = await GetPageAsync(category, date, page, PageSize, cancellationToken);
            ValidateRequest(category, date);
            var root = document.RootElement;
            var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.GetRawText())));
            if (!seenPages.Add(signature))
                throw new InvalidOperationException($"LibCal/categoría {category.Id}: la API repitió una página; extracción incompleta.");
            var bookings = new List<LibCalBooking>();
            foreach (var row in root.EnumerateArray())
            {
                RequireObject(row);
                VerifyFilter(row, "cid", category.Id);
                VerifyFilter(row, "lid", category.LocationId);
                bookings.Add(MapBooking(row, category));
            }
            bool finished = bookings.Count < PageSize;
            yield return new LibCalBookingPage(category, page, bookings, finished);
            if (finished) yield break;
        }
        throw new InvalidOperationException($"LibCal/categoría {category.Id}: se alcanzó el límite de páginas; extracción incompleta.");
    }

    private void ValidateRequest(LibCalCategory category, DateOnly date)
    {
        if (date != ExtractionPlanner.Today(clock.GetUtcNow()))
            throw new ArgumentException("LibCal: la extracción solo admite la fecha de hoy en America/Lima. No se cargará una fecha distinta.");
        if (!long.TryParse(category.Id, out var categoryId) || categoryId <= 0 ||
            !long.TryParse(category.LocationId, out var locationId) || locationId <= 0)
            throw new ArgumentException("LibCal: categoría y ubicación deben tener identificadores numéricos válidos.");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Configura LIBCAL_CLIENT_ID y LIBCAL_CLIENT_SECRET en la terminal; no los guardes en Git.");
    }

    private async Task<JsonDocument> GetPageAsync(
        LibCalCategory category, DateOnly date, int page, int limit, CancellationToken cancellationToken)
    {
        var query = $"lid={Uri.EscapeDataString(category.LocationId)}&cid={Uri.EscapeDataString(category.Id)}" +
            $"&date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&days=0&limit={limit}&page={page}" +
            "&form_answers=1&check_in_status=1&include_cancel=1&include_tentative=1&include_denied=1&internal_notes=true";
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var token = await GetTokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/space/bookings?{query}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                accessToken = null;
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"LibCal/categoría {category.Id}: HTTP {(int)response.StatusCode}. Revisa permisos de lectura de Spaces/Seats y límites de la API.");
            var document = await ParseJsonAsync(response, cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > limit)
            {
                document.Dispose();
                throw new InvalidOperationException("LibCal: respuesta de reservas inesperada. Se esperaba un array JSON respetando limit; revisa el contrato.");
            }
            return document;
        }
        throw new InvalidOperationException("LibCal: no se pudo autenticar la consulta.");
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (accessToken is not null && clock.GetUtcNow() < expiresAt) return accessToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/oauth/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["client_secret"] = clientSecret, ["grant_type"] = "client_credentials"
        });
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"LibCal: autenticación OAuth rechazada (HTTP {(int)response.StatusCode}). Revisa client ID/secret y permisos de la aplicación.");
        using var document = await ParseJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(token.GetString()) ||
            !Regex.IsMatch(token.GetString()!, @"^[A-Za-z0-9._~+/=-]+$", RegexOptions.CultureInvariant) ||
            !root.TryGetProperty("expires_in", out var expiration) || expiration.ValueKind != JsonValueKind.Number ||
            !expiration.TryGetInt32(out var seconds) || seconds <= 0)
            throw new InvalidOperationException("LibCal: respuesta OAuth inesperada; faltan access_token o expires_in válidos.");
        accessToken = token.GetString()!;
        expiresAt = clock.GetUtcNow().AddSeconds(Math.Max(1, seconds - 30));
        return accessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await httpClient.SendAsync(request, cancellationToken); }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("LibCal: no se pudo conectar por HTTPS. Revisa red, TLS y disponibilidad del servicio.");
        }
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("LibCal: la API devolvió un JSON inválido; no se procesó su contenido.");
        }
    }

    private static void RequireObject(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("LibCal: una reserva no es un objeto JSON.");
    }

    private static LibCalBooking MapBooking(JsonElement row, LibCalCategory category)
    {
        // LibCal returns each answer as q<ID>. Only group spaces ask for participants.
        var groupBooking = category.Name == "Espacios grupales";
        return new LibCalBooking(
            ReadId(row, "id"), ReadId(row, "bookId"),
            ReadDate(row, "fromDate"), ReadDate(row, "toDate"),
            ReadText(row, "email"), ReadText(row, "firstName"), ReadText(row, "lastName"),
            ReadText(row, "item_name"), NormalizeStatus(ReadText(row, "status")),
            NormalizeAttendance(ReadText(row, "check_in_status")),
            ReadAnswer(row, "q25458"),
            groupBooking ? ReadAnswer(row, "q25459") : null,
            groupBooking ? ReadAnswer(row, "q25460") : null,
            ReadLatestInternalNote(row));
    }

    private static string? ReadLatestInternalNote(JsonElement row)
    {
        if (!row.TryGetProperty("internal_notes", out var notes) || notes.ValueKind == JsonValueKind.Null)
            return null;
        if (notes.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("LibCal: internal_notes debe ser una lista.");

        string? latestText = null;
        var latestCreated = DateTimeOffset.MinValue;
        foreach (var note in notes.EnumerateArray())
        {
            if (note.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("LibCal: formato inesperado de nota interna.");
            var text = ReadText(note, "note");
            if (string.IsNullOrWhiteSpace(text)) continue;
            var created = ReadText(note, "created");
            if (!DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
                throw new InvalidOperationException("LibCal: fecha de nota interna inválida.");
            if (instant < latestCreated) continue;
            // When timestamps tie, use the last note in the response.
            latestCreated = instant;
            latestText = text;
        }
        return latestText;
    }

    private static string? NormalizeStatus(string? value)
    {
        var status = value?.Trim().ToLowerInvariant();
        if (status is not null &&
            Regex.IsMatch(status, @"^cancell?(ed|eb)($|\s+by\s+)", RegexOptions.CultureInvariant))
            return "Cancelado";

        return status switch
        {
            "confirmed" => "Confirmado",
            "tentative" => "Tentativo",
            "mediated tentative" => "Tentativo (mediación)",
            "denied" => "Denegado",
            "mediated denied" => "Denegado (mediación)",
            _ => value
        };
    }

    private static string? NormalizeAttendance(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "in" or "out" => "Asistió",
        "-" => "Sin registro de asistencia",
        _ => value
    };

    private static string? ReadAnswer(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null)
            return null;
        // Numeric phone answers can arrive as JSON numbers. Store them as text.
        var answer = name == "q25458" && field.ValueKind == JsonValueKind.Number
            ? field.GetRawText()
            : ReadText(row, name)?.Trim();
        return string.IsNullOrEmpty(answer) ? null : answer;
    }

    private static string? ReadText(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"LibCal: cambió el tipo del campo {name}; foto no confirmada.");
        return value.GetString();
    }

    private static string ReadId(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) ||
            string.IsNullOrWhiteSpace(value.ToString()))
            throw new InvalidOperationException($"LibCal: falta un identificador válido en {name}.");
        return value.ToString();
    }

    private static void VerifyFilter(JsonElement row, string name, string expected)
    {
        if (row.TryGetProperty(name, out _) && ReadId(row, name) != expected)
            throw new InvalidOperationException($"LibCal: la respuesta no respeta el filtro {name}; extracción cancelada.");
    }

    private static DateTimeOffset ReadDate(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"LibCal: falta una fecha válida en {name}.");
        var text = value.GetString()!;
        if (!Regex.IsMatch(text, @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidOperationException($"LibCal: {name} debe ser una fecha ISO con zona horaria explícita.");
        return date;
    }
}
