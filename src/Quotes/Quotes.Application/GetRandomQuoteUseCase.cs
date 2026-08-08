using Quotes.Domain;

namespace Quotes.Application;

public sealed record QuoteDto(string Id, string Text, string Author);

public interface IAuthValidationClient
{
    Task<AuthValidationResult> ValidateAsync(string accessToken, string correlationId, CancellationToken cancellationToken);
}

public sealed record AuthValidationResult(bool Valid, string? Username);

public interface IGetRandomQuoteUseCase
{
    Task<QuoteDto?> ExecuteAsync(string accessToken, string correlationId, CancellationToken cancellationToken);
}

public sealed class GetRandomQuoteUseCase : IGetRandomQuoteUseCase
{
    private readonly IAuthValidationClient _auth;
    private readonly IQuoteRepository _quotes;

    public GetRandomQuoteUseCase(IAuthValidationClient auth, IQuoteRepository quotes)
    {
        _auth = auth;
        _quotes = quotes;
    }

    public async Task<QuoteDto?> ExecuteAsync(string accessToken, string correlationId, CancellationToken cancellationToken)
    {
        var validation = await _auth.ValidateAsync(accessToken, correlationId, cancellationToken);
        if (!validation.Valid)
        {
            return null;
        }

        var quote = _quotes.GetRandom();
        return new QuoteDto(quote.Id, quote.Text, quote.Author);
    }
}
