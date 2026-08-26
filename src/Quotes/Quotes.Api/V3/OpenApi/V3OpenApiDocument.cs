namespace Quotes.Api.V3.OpenApi;

/// <summary>
/// Serves the OpenAPI document generated from <c>V3/Contracts/quotes_v3.proto</c>. The
/// transcoding runtime cannot produce one (transcoded routes are invisible to ApiExplorer),
/// so the freeze pipeline generates it from the contract itself — comments, google.api.http
/// rules and openapiv2 options — and this class hands the embedded bytes to
/// <c>/openapi/v3.json</c> and the Scalar picker.
/// </summary>
internal static class V3OpenApiDocument
{
    private static readonly Lazy<string> _json = new(() =>
    {
        using var stream = typeof(V3OpenApiDocument).Assembly.GetManifestResourceStream("quotes-v3.openapi.json");
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    });

    internal static string Json => _json.Value;
}
