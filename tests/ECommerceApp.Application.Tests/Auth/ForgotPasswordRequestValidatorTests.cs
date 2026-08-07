using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Auth.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Auth;

public class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _validator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var request = new ForgotPasswordRequest("jane@example.com");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Missing_or_malformed_email_is_rejected(string email)
    {
        var request = new ForgotPasswordRequest(email);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
