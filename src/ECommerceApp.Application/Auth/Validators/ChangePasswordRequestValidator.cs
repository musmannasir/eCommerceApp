using ECommerceApp.Application.Auth.Models;
using FluentValidation;

namespace ECommerceApp.Application.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(128)
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different from the current password.");
    }
}
