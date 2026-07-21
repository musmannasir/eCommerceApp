using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Inventory.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Inventory;

public class PurchaseOrderValidatorsTests
{
    private readonly CreatePurchaseOrderRequestValidator _createValidator = new();
    private readonly AddPurchaseOrderItemRequestValidator _itemValidator = new();
    private readonly ReceiveGoodsRequestValidator _receiveValidator = new();

    [Fact]
    public void A_well_formed_create_request_is_valid()
    {
        _createValidator.Validate(new CreatePurchaseOrderRequest(1, 1, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_create_request_with_no_supplier_is_rejected()
    {
        _createValidator.Validate(new CreatePurchaseOrderRequest(0, 1, null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_item_with_zero_quantity_is_rejected()
    {
        _itemValidator.Validate(new AddPurchaseOrderItemRequest(1, 1, 0, 5m)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_item_with_negative_unit_cost_is_rejected()
    {
        _itemValidator.Validate(new AddPurchaseOrderItemRequest(1, 1, 5, -1m)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_receive_request_with_no_lines_is_rejected()
    {
        _receiveValidator.Validate(new ReceiveGoodsRequest(1, Array.Empty<ReceiveGoodsLineRequest>(), null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_receive_request_with_a_zero_quantity_line_is_rejected()
    {
        var request = new ReceiveGoodsRequest(1, new[] { new ReceiveGoodsLineRequest(1, 0, false) }, null, null);

        _receiveValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_receive_request_is_valid()
    {
        var request = new ReceiveGoodsRequest(1, new[] { new ReceiveGoodsLineRequest(1, 5, false) }, "notes", null);

        _receiveValidator.Validate(request).IsValid.Should().BeTrue();
    }
}
