using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Auth.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Auth;

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var request = new ResetPasswordRequest("jane@example.com", "reset-token", "NewPassword1!");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "reset-token", "NewPassword1!")]
    [InlineData("not-an-email", "reset-token", "NewPassword1!")]
    [InlineData("jane@example.com", "", "NewPassword1!")]
    [InlineData("jane@example.com", "reset-token", "")]
    public void Missing_or_malformed_fields_are_rejected(string email, string token, string newPassword)
    {
        var request = new ResetPasswordRequest(email, token, newPassword);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
