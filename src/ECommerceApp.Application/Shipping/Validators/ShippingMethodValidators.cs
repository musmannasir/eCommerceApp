using ECommerceApp.Application.Shipping.Models;
using FluentValidation;

namespace ECommerceApp.Application.Shipping.Validators;

public class CreateShippingMethodRequestValidator : AbstractValidator<CreateShippingMethodRequest>
{
    public CreateShippingMethodRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2)
            .WithMessage("Country code must be a 2-letter ISO code (e.g. US, PK).");
        RuleFor(x => x.RegionCode).MaximumLength(10);
        RuleFor(x => x.BaseRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RatePerKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FreeShippingThreshold).GreaterThanOrEqualTo(0).When(x => x.FreeShippingThreshold.HasValue);
        RuleFor(x => x.EstimatedDeliveryDaysMin).GreaterThan(0).When(x => x.EstimatedDeliveryDaysMin.HasValue);
        RuleFor(x => x.EstimatedDeliveryDaysMax)
            .GreaterThanOrEqualTo(x => x.EstimatedDeliveryDaysMin!.Value)
            .When(x => x.EstimatedDeliveryDaysMin.HasValue && x.EstimatedDeliveryDaysMax.HasValue)
            .WithMessage("Maximum delivery days must be at least the minimum.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateShippingMethodRequestValidator : AbstractValidator<UpdateShippingMethodRequest>
{
    public UpdateShippingMethodRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2)
            .WithMessage("Country code must be a 2-letter ISO code (e.g. US, PK).");
        RuleFor(x => x.RegionCode).MaximumLength(10);
        RuleFor(x => x.BaseRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RatePerKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FreeShippingThreshold).GreaterThanOrEqualTo(0).When(x => x.FreeShippingThreshold.HasValue);
        RuleFor(x => x.EstimatedDeliveryDaysMin).GreaterThan(0).When(x => x.EstimatedDeliveryDaysMin.HasValue);
        RuleFor(x => x.EstimatedDeliveryDaysMax)
            .GreaterThanOrEqualTo(x => x.EstimatedDeliveryDaysMin!.Value)
            .When(x => x.EstimatedDeliveryDaysMin.HasValue && x.EstimatedDeliveryDaysMax.HasValue)
            .WithMessage("Maximum delivery days must be at least the minimum.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
