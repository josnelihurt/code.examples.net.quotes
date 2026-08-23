using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AspireQuotesPoc.Specs.Support;

/// <summary>
/// Scenario-scoped state, constructor-injected by Reqnroll's context injection. One HTTP
/// client per scenario, one correlation id per scenario, and the last response (parsed
/// when the body is JSON) for the shared Then steps to assert on.
/// </summary>
public sealed class ApiWorld
{
    /// <summary>Sent on every request so the echo can be asserted without per-step plumbing.</summary>
    private const string _correlationHeader = "X-Correlation-Id";

    public HttpClient Client { get; } = AspireStack.CreateGatewayClient();

    public string CorrelationId { get; } = $"bdd-{Guid.NewGuid():N}";

    public string? AccessToken { get; set; }

    public HttpResponseMessage? LastResponse { get; private set; }

    public JsonElement? LastBody { get; private set; }

    public string? LastCreatedLocation { get; private set; }

    /// <summary>Id of the quote the last successful publish created; lets scenarios fetch it back.</summary>
    public string? LastCreatedId { get; set; }

    /// <summary>The catalog is in-memory and POST mutates it, so every scenario mints its own text.</summary>
    public string UniqueText { get; } = $"Specification quote {Guid.NewGuid():N}.";

    public ApiWorld() => Client.DefaultRequestHeaders.Add(_correlationHeader, CorrelationId);

    /// <summary>Records one HTTP call for the Then steps, parsing the body when it is JSON.</summary>
    public async Task RecordAsync(HttpResponseMessage response)
    {
        LastResponse = response;
        LastCreatedLocation = response.Headers.Location?.ToString();
        LastBody = response.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json"
            ? await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync())
            : null;
    }

    /// <summary>JSON body for a request, matching the wire format the APIs document.</summary>
    public static StringContent JsonBody(string json) =>
        new(json, Encoding.UTF8, "application/json");

    /// <summary>Sets the bearer header used by authorized calls; null clears it.</summary>
    public void UseToken(string? accessToken) =>
        Client.DefaultRequestHeaders.Authorization =
            accessToken is null ? null : new AuthenticationHeaderValue("Bearer", accessToken);
}
