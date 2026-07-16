using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Auth.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var request = new RegisterRequest("jane@example.com", "any-password", "Jane", "Doe");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "password", "Jane", "Doe")]
    [InlineData("not-an-email", "password", "Jane", "Doe")]
    [InlineData("jane@example.com", "", "Jane", "Doe")]
    [InlineData("jane@example.com", "password", "", "Doe")]
    [InlineData("jane@example.com", "password", "Jane", "")]
    public void Missing_or_malformed_fields_are_rejected(string email, string password, string firstName, string lastName)
    {
        var request = new RegisterRequest(email, password, firstName, lastName);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
