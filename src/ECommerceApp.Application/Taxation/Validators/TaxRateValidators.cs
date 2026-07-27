using ECommerceApp.Application.Taxation.Models;
using FluentValidation;

namespace ECommerceApp.Application.Taxation.Validators;

public class CreateTaxRateRequestValidator : AbstractValidator<CreateTaxRateRequest>
{
    public CreateTaxRateRequestValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2)
            .WithMessage("Country code must be a 2-letter ISO code (e.g. US, PK).");
        RuleFor(x => x.RegionCode).MaximumLength(10);
        RuleFor(x => x.TaxCategory).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RatePercent).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

public class UpdateTaxRateRequestValidator : AbstractValidator<UpdateTaxRateRequest>
{
    public UpdateTaxRateRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2)
            .WithMessage("Country code must be a 2-letter ISO code (e.g. US, PK).");
        RuleFor(x => x.RegionCode).MaximumLength(10);
        RuleFor(x => x.TaxCategory).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RatePercent).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}
