using ECommerceApp.Application.Inventory.Models;
using FluentValidation;

namespace ECommerceApp.Application.Inventory.Validators;

public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class AddPurchaseOrderItemRequestValidator : AbstractValidator<AddPurchaseOrderItemRequest>
{
    public AddPurchaseOrderItemRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.QuantityOrdered).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public class ReceiveGoodsRequestValidator : AbstractValidator<ReceiveGoodsRequest>
{
    public ReceiveGoodsRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line must have a quantity to receive.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PurchaseOrderItemId).GreaterThan(0);
            line.RuleFor(l => l.QuantityReceived).GreaterThan(0);
        });
        RuleFor(x => x.OverrideReason).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
