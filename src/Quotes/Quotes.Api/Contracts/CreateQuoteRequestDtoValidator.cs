using FluentValidation;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Contracts;

public sealed class CreateQuoteRequestDtoValidator : AbstractValidator<CreateQuoteRequestDto>
{
    public CreateQuoteRequestDtoValidator()
    {
        // Transport-shape guards only; the domain stays the single source of catalog rules.
        RuleFor(x => x.Text).NotEmpty().MaximumLength(QuoteRules.MaxTextLength);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(QuoteRules.MaxAuthorLength);
    }
}
