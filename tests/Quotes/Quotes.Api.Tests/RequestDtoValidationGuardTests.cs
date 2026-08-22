using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Quotes.Api.Tests;

/// <summary>
/// Fail-closed backstop for the Data Annotations validation canon: framework
/// <c>AddValidation()</c> only validates what is annotated, so a request DTO without a
/// single validation attribute would silently pass everything to the layer below.
/// </summary>
public class RequestDtoValidationGuardTests
{
    [Fact]
    public void Every_request_dto_declares_at_least_one_validation_attribute()
    {
        var requestDtos = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.IsClass && type.IsPublic && type.Name.EndsWith("RequestDto"))
            .ToList();

        requestDtos.ShouldNotBeEmpty("the guard is meaningless if no request DTOs exist");

        foreach (var dto in requestDtos)
        {
            var annotated = dto
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(property => property.GetCustomAttributes(inherit: false))
                .Any(attribute => attribute is ValidationAttribute);

            annotated.ShouldBeTrue(
                $"{dto.Name} must declare at least one validation attribute: " +
                "unannotated body DTOs bypass AddValidation() entirely (fail-open).");
        }
    }
}
