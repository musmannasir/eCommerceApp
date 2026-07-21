using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Marketing;
using FluentValidation;

namespace ECommerceApp.Application.Marketing.Validators;

public class CreateHomePageBannerRequestValidator : AbstractValidator<CreateHomePageBannerRequest>
{
    public CreateHomePageBannerRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(500);
        RuleFor(x => x.LinkUrl).MaximumLength(500);
        RuleFor(x => x.BannerType).NotEmpty().Must(BeAValidBannerType).WithMessage("Banner type must be 'Hero' or 'Promo'.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }

    private static bool BeAValidBannerType(string value) => Enum.TryParse<BannerType>(value, out _);
}

public class UpdateHomePageBannerRequestValidator : AbstractValidator<UpdateHomePageBannerRequest>
{
    public UpdateHomePageBannerRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(500);
        RuleFor(x => x.LinkUrl).MaximumLength(500);
        RuleFor(x => x.BannerType).NotEmpty().Must(BeAValidBannerType).WithMessage("Banner type must be 'Hero' or 'Promo'.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }

    private static bool BeAValidBannerType(string value) => Enum.TryParse<BannerType>(value, out _);
}
