using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Auth.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Auth;

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void A_new_password_different_from_the_current_one_is_valid()
    {
        var request = new ChangePasswordRequest("user-1", "OldPassword1!", "NewPassword1!");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_new_password_identical_to_the_current_one_is_rejected()
    {
        var request = new ChangePasswordRequest("user-1", "SamePassword1!", "SamePassword1!");

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_user_id_is_rejected()
    {
        var request = new ChangePasswordRequest("", "OldPassword1!", "NewPassword1!");

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
