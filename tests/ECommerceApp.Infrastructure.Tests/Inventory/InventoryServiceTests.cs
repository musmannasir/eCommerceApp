using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Inventory;

public class InventoryServiceTests : IDisposable
{
    private readonly InventoryTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Recording_opening_stock_creates_an_inventory_item_and_an_opening_stock_movement()
    {
        var (warehouseId, productId, _) = await SeedProductAsync();

        var result = await _harness.InventoryService.RecordOpeningStockAsync(
            new RecordOpeningStockRequest(warehouseId, productId, null, 50, 10, 20, false));

        result.IsSuccess.Should().BeTrue();
        result.Value.QuantityOnHand.Should().Be(50);
        result.Value.QuantityAvailable.Should().Be(50);
        result.Value.StockStatus.Should().Be(nameof(StockStatus.InStock));

        var movements = await _harness.InventoryService.GetMovementHistoryAsync(result.Value.Id, new() { PageSize = 10 });
        movements.Value.Items.Should().ContainSingle(m => m.MovementType == nameof(StockMovementType.OpeningStock) && m.QuantityChange == 50);
    }

    [Fact]
    public async Task Recording_opening_stock_twice_for_the_same_product_and_warehouse_is_rejected()
    {
        var (warehouseId, productId, _) = await SeedProductAsync();
        await _harness.InventoryService.RecordOpeningStockAsync(new RecordOpeningStockRequest(warehouseId, productId, null, 10, 0, 0, false));

        var result = await _harness.InventoryService.RecordOpeningStockAsync(new RecordOpeningStockRequest(warehouseId, productId, null, 5, 0, 0, false));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Adjusting_stock_upward_increases_on_hand_and_records_a_movement()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 10, reorderLevel: 2);

        var result = await _harness.InventoryService.AdjustStockAsync(new AdjustStockRequest(itemId, 15, "Cycle count correction"));

        result.IsSuccess.Should().BeTrue();
        result.Value.QuantityOnHand.Should().Be(25);

        var movements = await _harness.InventoryService.GetMovementHistoryAsync(itemId, new() { PageSize = 10 });
        movements.Value.Items.Should().Contain(m => m.MovementType == nameof(StockMovementType.ManualAdjustment) && m.QuantityChange == 15);
    }

    [Fact]
    public async Task Adjusting_stock_downward_decreases_on_hand()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 10, reorderLevel: 2);

        var result = await _harness.InventoryService.AdjustStockAsync(new AdjustStockRequest(itemId, -4, "Damaged in warehouse"));

        result.IsSuccess.Should().BeTrue();
        result.Value.QuantityOnHand.Should().Be(6);
    }

    [Fact]
    public async Task Adjusting_stock_below_zero_on_hand_is_rejected()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 5, reorderLevel: 2);

        var result = await _harness.InventoryService.AdjustStockAsync(new AdjustStockRequest(itemId, -10, "Too much"));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Reserving_stock_reduces_available_quantity_and_records_a_movement()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 5);

        var result = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 8, "Cart", "cart-123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(ReservationStatus.Active));

        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(8);
        item.Value.QuantityAvailable.Should().Be(12);

        var movements = await _harness.InventoryService.GetMovementHistoryAsync(itemId, new() { PageSize = 10 });
        movements.Value.Items.Should().Contain(m => m.MovementType == nameof(StockMovementType.SaleReservation) && m.QuantityChange == 8);
    }

    [Fact]
    public async Task Releasing_a_reservation_restores_available_quantity()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 5);
        var reservation = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 8, null, null));

        var releaseResult = await _harness.InventoryService.ReleaseReservationAsync(reservation.Value.Id);

        releaseResult.IsSuccess.Should().BeTrue();
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(0);
        item.Value.QuantityAvailable.Should().Be(20);

        var movements = await _harness.InventoryService.GetMovementHistoryAsync(itemId, new() { PageSize = 10 });
        movements.Value.Items.Should().Contain(m => m.MovementType == nameof(StockMovementType.ReservationRelease) && m.QuantityChange == -8);
    }

    [Fact]
    public async Task Consuming_a_reservation_deducts_on_hand_quantity_and_records_a_movement()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 5);
        var reservation = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 8, "Order", "order-123"));

        var consumeResult = await _harness.InventoryService.ConsumeReservationAsync(reservation.Value.Id);

        consumeResult.IsSuccess.Should().BeTrue();
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityOnHand.Should().Be(12);
        item.Value.QuantityReserved.Should().Be(0);
        item.Value.QuantityAvailable.Should().Be(12);

        var movements = await _harness.InventoryService.GetMovementHistoryAsync(itemId, new() { PageSize = 10 });
        movements.Value.Items.Should().Contain(m => m.MovementType == nameof(StockMovementType.SaleCompletion) && m.QuantityChange == -8);
    }

    [Fact]
    public async Task Consuming_an_already_released_reservation_is_rejected()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 5);
        var reservation = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 8, null, null));
        await _harness.InventoryService.ReleaseReservationAsync(reservation.Value.Id);

        var consumeResult = await _harness.InventoryService.ConsumeReservationAsync(reservation.Value.Id);

        consumeResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Reserving_more_than_available_without_backorder_is_rejected()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 5, reorderLevel: 1, allowBackorder: false);

        var result = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 10, null, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Reserving_more_than_available_with_backorder_allowed_succeeds()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 5, reorderLevel: 1, allowBackorder: true);

        var result = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 10, null, null));

        result.IsSuccess.Should().BeTrue();
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityAvailable.Should().Be(-5);
        item.Value.StockStatus.Should().Be(nameof(StockStatus.Backorder));
    }

    [Fact]
    public async Task Stock_status_becomes_low_stock_once_available_quantity_falls_to_the_reorder_level()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 10, reorderLevel: 5, allowBackorder: false);

        await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 5, null, null));

        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityAvailable.Should().Be(5);
        item.Value.StockStatus.Should().Be(nameof(StockStatus.LowStock));
    }

    [Fact]
    public async Task Stock_status_becomes_out_of_stock_when_available_quantity_reaches_zero_without_backorder()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 5, reorderLevel: 1, allowBackorder: false);

        await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 5, null, null));

        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityAvailable.Should().Be(0);
        item.Value.StockStatus.Should().Be(nameof(StockStatus.OutOfStock));
    }

    [Fact]
    public async Task Movement_history_accumulates_every_change_in_order_and_never_shrinks()
    {
        var itemId = await SeedInventoryItemAsync(quantity: 10, reorderLevel: 2);

        await _harness.InventoryService.AdjustStockAsync(new AdjustStockRequest(itemId, 5, "Received extra stock"));
        var reservation = await _harness.InventoryService.ReserveStockAsync(new ReserveStockRequest(itemId, 3, null, null));
        await _harness.InventoryService.ReleaseReservationAsync(reservation.Value.Id);

        var movements = await _harness.InventoryService.GetMovementHistoryAsync(itemId, new() { PageSize = 10 });

        // Opening stock + manual adjustment + reservation + release = 4 immutable rows, oldest-created data untouched.
        movements.Value.Items.Should().HaveCount(4);
        movements.Value.Items.Select(m => m.MovementType).Should().Contain(new[]
        {
            nameof(StockMovementType.OpeningStock),
            nameof(StockMovementType.ManualAdjustment),
            nameof(StockMovementType.SaleReservation),
            nameof(StockMovementType.ReservationRelease),
        });
    }

    private async Task<(int WarehouseId, int ProductId, int? VariantId)> SeedProductAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = "Widget",
            Slug = $"widget-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = true,
        };
        _harness.DbContext.Products.Add(product);

        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Warehouses.Add(warehouse);

        await _harness.DbContext.SaveChangesAsync();

        return (warehouse.Id, product.Id, null);
    }

    private async Task<int> SeedInventoryItemAsync(int quantity, int reorderLevel, bool allowBackorder = false)
    {
        var (warehouseId, productId, _) = await SeedProductAsync();
        var result = await _harness.InventoryService.RecordOpeningStockAsync(
            new RecordOpeningStockRequest(warehouseId, productId, null, quantity, reorderLevel, reorderLevel * 2, allowBackorder));

        return result.Value.Id;
    }
}
