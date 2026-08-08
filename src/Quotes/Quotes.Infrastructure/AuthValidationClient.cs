using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Quotes.Application;

namespace Quotes.Infrastructure;

public sealed class AuthValidationClient : IAuthValidationClient
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthValidationClient> _logger;

    public AuthValidationClient(HttpClient httpClient, ILogger<AuthValidationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AuthValidationResult> ValidateAsync(string accessToken, string correlationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeaderName, correlationId);
        request.Content = JsonContent.Create(new { accessToken });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auth validate returned {StatusCode}", (int)response.StatusCode);
                return new AuthValidationResult(false, null);
            }

            var payload = await response.Content.ReadFromJsonAsync<ValidateResponse>(cancellationToken);
            if (payload is null || !payload.Valid)
            {
                return new AuthValidationResult(false, null);
            }

            return new AuthValidationResult(true, payload.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Auth validate endpoint");
            return new AuthValidationResult(false, null);
        }
    }

    private sealed record ValidateResponse(
        [property: JsonPropertyName("valid")] bool Valid,
        [property: JsonPropertyName("username")] string? Username);
}
