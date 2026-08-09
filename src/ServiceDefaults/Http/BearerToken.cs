namespace AspireQuotesPoc.Http;

public static class BearerToken
{
    private const string Prefix = "Bearer ";

    public static bool TryParse(string? authorizationHeader, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = authorizationHeader[Prefix.Length..].Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        token = candidate;
        return true;
    }
}
