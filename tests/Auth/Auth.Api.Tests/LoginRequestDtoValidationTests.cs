using System.ComponentModel.DataAnnotations;
using Auth.Api.Contracts;

namespace Auth.Api.Tests;

public class LoginRequestDtoValidationTests
{
    private static List<ValidationResult> Validate(LoginRequestDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var results = Validate(new LoginRequestDto { Username = "jrb", Password = "supersecret" });

        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "supersecret", nameof(LoginRequestDto.Username))]
    [InlineData("jrb", "", nameof(LoginRequestDto.Password))]
    public void Empty_fields_are_reported_against_the_right_property(string username, string password, string expectedProperty)
    {
        var results = Validate(new LoginRequestDto { Username = username, Password = password });

        results.ShouldNotBeEmpty();
        results.SelectMany(r => r.MemberNames).ShouldContain(expectedProperty);
    }

    [Fact]
    public void Username_longer_than_one_hundred_characters_is_rejected()
    {
        var results = Validate(new LoginRequestDto
        {
            Username = new string('u', LoginRequestDto.MaxUsernameLength + 1),
            Password = "supersecret"
        });

        results.ShouldNotBeEmpty();
        results.SelectMany(r => r.MemberNames).ShouldContain(nameof(LoginRequestDto.Username));
    }

    [Fact]
    public void Password_longer_than_two_hundred_characters_is_rejected()
    {
        var results = Validate(new LoginRequestDto
        {
            Username = "jrb",
            Password = new string('p', LoginRequestDto.MaxPasswordLength + 1)
        });

        results.ShouldNotBeEmpty();
        results.SelectMany(r => r.MemberNames).ShouldContain(nameof(LoginRequestDto.Password));
    }

    [Fact]
    public void Values_at_the_length_boundary_are_accepted()
    {
        var results = Validate(new LoginRequestDto
        {
            Username = new string('u', LoginRequestDto.MaxUsernameLength),
            Password = new string('p', LoginRequestDto.MaxPasswordLength)
        });

        results.ShouldBeEmpty();
    }
}
