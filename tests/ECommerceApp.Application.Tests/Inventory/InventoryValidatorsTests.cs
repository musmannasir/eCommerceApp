using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Inventory.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Inventory;

public class InventoryValidatorsTests
{
    [Fact]
    public void A_well_formed_warehouse_request_is_valid()
    {
        var request = new CreateWarehouseRequest("Main", "WH1", null, null, null, null, null, null, true, true);

        new CreateWarehouseRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_warehouse_request_without_a_code_is_rejected()
    {
        var request = new CreateWarehouseRequest("Main", "", null, null, null, null, null, null, true, true);

        new CreateWarehouseRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_warehouse_update_is_valid()
    {
        var request = new UpdateWarehouseRequest(1, "Main", "WH1", null, null, null, null, null, null, true, true);

        new UpdateWarehouseRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_id_is_rejected_on_warehouse_update()
    {
        var request = new UpdateWarehouseRequest(0, "Main", "WH1", null, null, null, null, null, null, true, true);

        new UpdateWarehouseRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_opening_stock_request_is_valid()
    {
        var request = new RecordOpeningStockRequest(1, 1, null, 10, 5, 20, false);

        new RecordOpeningStockRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_negative_opening_stock_quantity_is_rejected()
    {
        var request = new RecordOpeningStockRequest(1, 1, null, -1, 5, 20, false);

        new RecordOpeningStockRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_zero_adjustment_quantity_is_rejected()
    {
        var request = new AdjustStockRequest(1, 0, "No-op");

        new AdjustStockRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_adjustment_without_a_reason_is_rejected()
    {
        var request = new AdjustStockRequest(1, 5, "");

        new AdjustStockRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_reservation_request_with_zero_quantity_is_rejected()
    {
        var request = new ReserveStockRequest(1, 0, null, null);

        new ReserveStockRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }
}
