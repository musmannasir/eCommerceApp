using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Marketing;
using FluentValidation;

namespace ECommerceApp.Application.Marketing.Validators;

public class CreatePromotionRequestValidator : AbstractValidator<CreatePromotionRequest>
{
    public CreatePromotionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CouponCode).MaximumLength(50);

        RuleFor(x => x.DiscountType).NotEmpty().Must(BeAValidDiscountType)
            .WithMessage("Discount type must be 'Percentage' or 'FixedAmount'.");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
            .When(x => x.DiscountType == nameof(PromotionDiscountType.Percentage))
            .WithMessage("A percentage discount cannot exceed 100.");

        RuleFor(x => x.ScopeType).NotEmpty().Must(BeAValidScopeType)
            .WithMessage("Scope type must be 'EntireOrder', 'Category', 'Brand', or 'Product'.");
        RuleFor(x => x.ScopeCategoryId).NotNull()
            .When(x => x.ScopeType == nameof(PromotionScopeType.Category))
            .WithMessage("A category must be selected for a category-scoped promotion.");
        RuleFor(x => x.ScopeBrandId).NotNull()
            .When(x => x.ScopeType == nameof(PromotionScopeType.Brand))
            .WithMessage("A brand must be selected for a brand-scoped promotion.");
        RuleFor(x => x.ScopeProductId).NotNull()
            .When(x => x.ScopeType == nameof(PromotionScopeType.Product))
            .WithMessage("A product must be selected for a product-scoped promotion.");

        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinimumOrderAmount.HasValue);
        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
        RuleFor(x => x.MaxTotalUses).GreaterThan(0).When(x => x.MaxTotalUses.HasValue);
        RuleFor(x => x.MaxUsesPerCustomer).GreaterThan(0).When(x => x.MaxUsesPerCustomer.HasValue);
        RuleFor(x => x.EndsAtUtc).GreaterThan(x => x.StartsAtUtc).When(x => x.EndsAtUtc.HasValue)
            .WithMessage("End date must be after the start date.");
    }

    internal static bool BeAValidDiscountType(string value) => Enum.TryParse<PromotionDiscountType>(value, out _);
    internal static bool BeAValidScopeType(string value) => Enum.TryParse<PromotionScopeType>(value, out _);
}

public class UpdatePromotionRequestValidator : AbstractValidator<UpdatePromotionRequest>
{
    public UpdatePromotionRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CouponCode).MaximumLength(50);

        RuleFor(x => x.DiscountType).NotEmpty().Must(CreatePromotionRequestValidator.BeAValidDiscountType)
            .WithMessage("Discount type must be 'Percentage' or 'FixedAmount'.");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
            .When(x => x.DiscountType == nameof(PromotionDiscountType.Percentage))
            .WithMessage("A percentage discount cannot exceed 100.");

        RuleFor(x => x.ScopeType).NotEmpty().Must(CreatePromotionRequestValidator.BeAValidScopeType)
            .WithMessage("Scope type must be 'EntireOrder', 'Category', 'Brand', or 'Product'.");
        RuleFor(x => x.ScopeCategoryId).NotNull()
            .When(x => x.ScopeType == nameof(PromotionScopeType.Category))
            .WithMessage("A category must be selected for a category-scoped promotion.");
        RuleFor(x => x.ScopeBrandId).NotNull()
            .When(x => x.ScopeType == nameof(PromotionScopeType.Brand))
            .WithMessage("A brand must be selected for a brand-scoped promotion.");
        RuleFor(x => x.ScopeProductId).NotNull()
            .When(x => x.ScopeType == nameof(PromotionScopeType.Product))
            .WithMessage("A product must be selected for a product-scoped promotion.");

        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinimumOrderAmount.HasValue);
        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
        RuleFor(x => x.MaxTotalUses).GreaterThan(0).When(x => x.MaxTotalUses.HasValue);
        RuleFor(x => x.MaxUsesPerCustomer).GreaterThan(0).When(x => x.MaxUsesPerCustomer.HasValue);
        RuleFor(x => x.EndsAtUtc).GreaterThan(x => x.StartsAtUtc).When(x => x.EndsAtUtc.HasValue)
            .WithMessage("End date must be after the start date.");
    }
}
