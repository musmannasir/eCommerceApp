using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Auth.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var request = new LoginRequest("jane@example.com", "any-password");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("not-an-email", "password")]
    [InlineData("jane@example.com", "")]
    public void Missing_or_malformed_fields_are_rejected(string email, string password)
    {
        var request = new LoginRequest(email, password);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
