using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Inventory;

public class PurchaseOrderServiceTests : IDisposable
{
    private readonly InventoryTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_purchase_order_assigns_a_draft_status_and_an_order_number()
    {
        var (supplierId, warehouseId, _) = await SeedAsync();

        var result = await _harness.PurchaseOrderService.CreateAsync(new CreatePurchaseOrderRequest(supplierId, warehouseId, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(PurchaseOrderStatus.Draft));
        result.Value.OrderNumber.Should().StartWith("PO-");
    }

    [Fact]
    public async Task Adding_an_item_to_a_draft_order_succeeds()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateOrderAsync(supplierId, warehouseId);

        var result = await _harness.PurchaseOrderService.AddItemAsync(new AddPurchaseOrderItemRequest(order.Id, productId, 10, 5m));

        result.IsSuccess.Should().BeTrue();
        result.Value.QuantityOrdered.Should().Be(10);
        result.Value.QuantityReceived.Should().Be(0);
    }

    [Fact]
    public async Task Adding_an_item_to_a_non_draft_order_is_rejected()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateOrderAsync(supplierId, warehouseId);
        await _harness.PurchaseOrderService.AddItemAsync(new AddPurchaseOrderItemRequest(order.Id, productId, 10, 5m));
        await _harness.PurchaseOrderService.SubmitAsync(order.Id);

        var result = await _harness.PurchaseOrderService.AddItemAsync(new AddPurchaseOrderItemRequest(order.Id, productId, 5, 5m));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Submitting_an_order_with_no_items_is_rejected()
    {
        var (supplierId, warehouseId, _) = await SeedAsync();
        var order = await CreateOrderAsync(supplierId, warehouseId);

        var result = await _harness.PurchaseOrderService.SubmitAsync(order.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Full_lifecycle_draft_to_submitted_to_approved_succeeds()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateOrderAsync(supplierId, warehouseId);
        await _harness.PurchaseOrderService.AddItemAsync(new AddPurchaseOrderItemRequest(order.Id, productId, 10, 5m));

        var submit = await _harness.PurchaseOrderService.SubmitAsync(order.Id);
        submit.IsSuccess.Should().BeTrue();

        var approve = await _harness.PurchaseOrderService.ApproveAsync(order.Id);
        approve.IsSuccess.Should().BeTrue();

        var loaded = await _harness.PurchaseOrderService.GetByIdAsync(order.Id);
        loaded.Value.Status.Should().Be(nameof(PurchaseOrderStatus.Approved));
    }

    [Fact]
    public async Task Approving_a_draft_order_is_rejected()
    {
        var (supplierId, warehouseId, _) = await SeedAsync();
        var order = await CreateOrderAsync(supplierId, warehouseId);

        var result = await _harness.PurchaseOrderService.ApproveAsync(order.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Cancelling_an_approved_order_succeeds()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);

        var result = await _harness.PurchaseOrderService.CancelAsync(order.Id);

        result.IsSuccess.Should().BeTrue();
        var loaded = await _harness.PurchaseOrderService.GetByIdAsync(order.Id);
        loaded.Value.Status.Should().Be(nameof(PurchaseOrderStatus.Cancelled));
    }

    [Fact]
    public async Task Cancelling_a_partially_received_order_is_rejected()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var item = order.Items[0];
        await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            order.Id, new[] { new ReceiveGoodsLineRequest(item.Id, 4, false) }, null, null));

        var result = await _harness.PurchaseOrderService.CancelAsync(order.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Fully_receiving_an_order_marks_it_received_and_increases_stock()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var item = order.Items[0];

        var result = await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            order.Id, new[] { new ReceiveGoodsLineRequest(item.Id, 10, false) }, "All received", null));

        result.IsSuccess.Should().BeTrue();
        var loaded = await _harness.PurchaseOrderService.GetByIdAsync(order.Id);
        loaded.Value.Status.Should().Be(nameof(PurchaseOrderStatus.Received));
        loaded.Value.Items[0].QuantityReceived.Should().Be(10);

        var overview = await _harness.InventoryService.GetOverviewAsync(new InventoryItemQuery { WarehouseId = warehouseId });
        overview.Value.Items.Should().ContainSingle(i => i.ProductId == productId && i.QuantityOnHand == 10);
    }

    [Fact]
    public async Task Partially_receiving_an_order_marks_it_partially_received()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var item = order.Items[0];

        var result = await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            order.Id, new[] { new ReceiveGoodsLineRequest(item.Id, 4, false) }, null, null));

        result.IsSuccess.Should().BeTrue();
        var loaded = await _harness.PurchaseOrderService.GetByIdAsync(order.Id);
        loaded.Value.Status.Should().Be(nameof(PurchaseOrderStatus.PartiallyReceived));
        loaded.Value.Items[0].QuantityReceived.Should().Be(4);
    }

    [Fact]
    public async Task Receiving_more_than_outstanding_without_override_is_rejected()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var item = order.Items[0];

        var result = await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            order.Id, new[] { new ReceiveGoodsLineRequest(item.Id, 15, false) }, null, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Receiving_more_than_outstanding_with_override_but_no_reason_is_rejected()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var item = order.Items[0];

        var result = await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            order.Id, new[] { new ReceiveGoodsLineRequest(item.Id, 15, true) }, null, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("purchase_order.override_reason_required");
    }

    [Fact]
    public async Task Receiving_more_than_outstanding_with_override_and_reason_succeeds()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var order = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var item = order.Items[0];

        var result = await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            order.Id, new[] { new ReceiveGoodsLineRequest(item.Id, 15, true) }, null, "Supplier sent extra units by agreement"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.IsOverride);
    }

    [Fact]
    public async Task Receiving_against_a_line_from_a_different_order_is_rejected()
    {
        var (supplierId, warehouseId, productId) = await SeedAsync();
        var orderA = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);
        var orderB = await CreateApprovedOrderAsync(supplierId, warehouseId, productId, 10, 5m);

        var result = await _harness.PurchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            orderA.Id, new[] { new ReceiveGoodsLineRequest(orderB.Items[0].Id, 5, false) }, null, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    private async Task<PurchaseOrderDto> CreateOrderAsync(int supplierId, int warehouseId)
    {
        var result = await _harness.PurchaseOrderService.CreateAsync(new CreatePurchaseOrderRequest(supplierId, warehouseId, null, null));
        return result.Value;
    }

    private async Task<PurchaseOrderDto> CreateApprovedOrderAsync(int supplierId, int warehouseId, int productId, int quantity, decimal unitCost)
    {
        var order = await CreateOrderAsync(supplierId, warehouseId);
        await _harness.PurchaseOrderService.AddItemAsync(new AddPurchaseOrderItemRequest(order.Id, productId, quantity, unitCost));
        await _harness.PurchaseOrderService.SubmitAsync(order.Id);
        await _harness.PurchaseOrderService.ApproveAsync(order.Id);
        return (await _harness.PurchaseOrderService.GetByIdAsync(order.Id)).Value;
    }

    private async Task<(int SupplierId, int WarehouseId, int ProductId)> SeedAsync()
    {
        var supplier = new Supplier { Name = "Acme", Code = $"S-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Suppliers.Add(supplier);

        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Warehouses.Add(warehouse);

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
        await _harness.DbContext.SaveChangesAsync();

        return (supplier.Id, warehouse.Id, product.Id);
    }
}
