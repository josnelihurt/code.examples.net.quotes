using Auth.Api.Contracts;

namespace Auth.Api.Tests;

public class LoginRequestDtoValidatorTests
{
    private readonly LoginRequestDtoValidator _sut = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var result = _sut.Validate(new LoginRequestDto { Username = "jrb", Password = "supersecret" });

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "supersecret", nameof(LoginRequestDto.Username))]
    [InlineData("jrb", "", nameof(LoginRequestDto.Password))]
    public void Empty_fields_are_reported_against_the_right_property(string username, string password, string expectedProperty)
    {
        var result = _sut.Validate(new LoginRequestDto { Username = username, Password = password });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(expectedProperty);
    }

    [Fact]
    public void Username_longer_than_one_hundred_characters_is_rejected()
    {
        var result = _sut.Validate(new LoginRequestDto
        {
            Username = new string('u', 101),
            Password = "supersecret"
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(nameof(LoginRequestDto.Username));
    }

    [Fact]
    public void Password_longer_than_two_hundred_characters_is_rejected()
    {
        var result = _sut.Validate(new LoginRequestDto
        {
            Username = "jrb",
            Password = new string('p', 201)
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(nameof(LoginRequestDto.Password));
    }

    [Fact]
    public void Values_at_the_length_boundary_are_accepted()
    {
        var result = _sut.Validate(new LoginRequestDto
        {
            Username = new string('u', 100),
            Password = new string('p', 200)
        });

        result.IsValid.ShouldBeTrue();
    }
}
