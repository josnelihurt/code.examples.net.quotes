using FluentValidation;
using Quotes.Domain;

namespace Quotes.Api.Contracts;

public sealed class CreateQuoteRequestDtoValidator : AbstractValidator<CreateQuoteRequestDto>
{
    public CreateQuoteRequestDtoValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Quote.MaxTextLength);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(Quote.MaxAuthorLength);
    }
}
