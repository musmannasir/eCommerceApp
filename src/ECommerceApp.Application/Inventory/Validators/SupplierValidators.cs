using ECommerceApp.Application.Inventory.Models;
using FluentValidation;

namespace ECommerceApp.Application.Inventory.Validators;

public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Website).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Website).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class LinkSupplierProductRequestValidator : AbstractValidator<LinkSupplierProductRequest>
{
    public LinkSupplierProductRequestValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.SupplierSku).MaximumLength(100);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0).When(x => x.LeadTimeDays.HasValue);
    }
}

public class UpdateSupplierProductRequestValidator : AbstractValidator<UpdateSupplierProductRequest>
{
    public UpdateSupplierProductRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SupplierSku).MaximumLength(100);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0).When(x => x.LeadTimeDays.HasValue);
    }
}
