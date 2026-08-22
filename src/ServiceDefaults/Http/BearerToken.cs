namespace AspireQuotesPoc.Http;

public static class BearerToken
{
    private const string _prefix = "Bearer ";

    public static bool TryParse(string? authorizationHeader, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = authorizationHeader[_prefix.Length..].Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        token = candidate;
        return true;
    }
}
