using ECommerceApp.Application.Auth.Models;
using FluentValidation;

namespace ECommerceApp.Application.Auth.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(128);
    }
}
