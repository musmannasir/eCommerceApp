using ECommerceApp.Application.Catalog.Models;
using FluentValidation;

namespace ECommerceApp.Application.Catalog.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.BaseSKU).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThan(0).WithMessage("Selling price must be greater than zero.");
        RuleFor(x => x.CompareAtPrice)
            .GreaterThan(x => x.SellingPrice)
            .When(x => x.CompareAtPrice.HasValue)
            .WithMessage("Compare-at price must be greater than the selling price.");
        RuleFor(x => x.TaxCategory).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.BaseSKU).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThan(0).WithMessage("Selling price must be greater than zero.");
        RuleFor(x => x.CompareAtPrice)
            .GreaterThan(x => x.SellingPrice)
            .When(x => x.CompareAtPrice.HasValue)
            .WithMessage("Compare-at price must be greater than the selling price.");
        RuleFor(x => x.TaxCategory).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}

public class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.SellingPrice).GreaterThan(0).When(x => x.SellingPrice.HasValue);
        RuleFor(x => x.CompareAtPrice)
            .GreaterThan(x => x.SellingPrice!.Value)
            .When(x => x.CompareAtPrice.HasValue && x.SellingPrice.HasValue)
            .WithMessage("Compare-at price must be greater than the selling price.");
        RuleFor(x => x.AttributeValueIds).NotEmpty().WithMessage("Select at least one attribute value for the variant.");
    }
}

public class CreateSpecificationRequestValidator : AbstractValidator<CreateSpecificationRequest>
{
    public CreateSpecificationRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(1000);
    }
}
