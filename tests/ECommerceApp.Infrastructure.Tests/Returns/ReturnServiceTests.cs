using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Returns;

public class ReturnServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task SubmitReturnRequestAsync_succeeds_for_a_delivered_order()
    {
        var order = await CreateDeliveredOrderAsync("user-1");

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, "Arrived broken",
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ReturnRequestStatus.Requested);
        result.Value.OrderNumber.Should().Be(order.OrderNumber);
        result.Value.Items.Should().ContainSingle(i => i.OrderItemId == order.Items[0].Id && i.Quantity == 1);
    }

    [Fact]
    public async Task SubmitReturnRequestAsync_rejects_a_non_delivered_order()
    {
        var order = await CreatePaidOrderAsync("user-1");

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task SubmitReturnRequestAsync_returns_not_found_for_another_users_order()
    {
        var order = await CreateDeliveredOrderAsync("user-1");

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-2", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task SubmitReturnRequestAsync_rejects_a_quantity_exceeding_the_ordered_quantity()
    {
        var order = await CreateDeliveredOrderAsync("user-1");

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, order.Items[0].Quantity + 1) }));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task SubmitReturnRequestAsync_rejects_an_empty_item_list()
    {
        var order = await CreateDeliveredOrderAsync("user-1");

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem>()));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task SubmitReturnRequestAsync_rejects_a_second_open_request_for_the_same_order()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task SubmitReturnRequestAsync_allows_resubmission_after_a_prior_request_was_rejected()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var first = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));
        await _harness.ReturnService.RejectAsync(first.Value.Id, "Item is outside the return policy.");

        var result = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.NoLongerNeeded, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_marks_a_requested_request_approved()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        var result = await _harness.ReturnService.ApproveAsync(submitted.Value.Id);

        result.IsSuccess.Should().BeTrue();
        var requests = await _harness.ReturnService.GetReturnRequestsForOrderAsync(
            (await _harness.OrderService.GetByOrderNumberAsync("user-1", order.OrderNumber)).Value.Id);
        requests.Should().ContainSingle(r => r.Status == ReturnRequestStatus.Approved);
    }

    [Fact]
    public async Task ApproveAsync_rejects_a_request_that_is_not_pending()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(submitted.Value.Id);

        var result = await _harness.ReturnService.ApproveAsync(submitted.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task RejectAsync_marks_a_requested_request_rejected_with_a_reason()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        var result = await _harness.ReturnService.RejectAsync(submitted.Value.Id, "Outside the return window.");

        result.IsSuccess.Should().BeTrue();
        var orderDto = await _harness.OrderService.GetByOrderNumberAsync("user-1", order.OrderNumber);
        orderDto.Value.ReturnRequests.Should().ContainSingle(r =>
            r.Status == ReturnRequestStatus.Rejected && r.RejectionReason == "Outside the return window.");
    }

    [Fact]
    public async Task RejectAsync_rejects_a_request_that_is_not_pending()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));
        await _harness.ReturnService.RejectAsync(submitted.Value.Id, "Outside the return window.");

        var result = await _harness.ReturnService.RejectAsync(submitted.Value.Id, "Second attempt.");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task GetPendingQueueAsync_only_returns_requested_requests_and_paginates()
    {
        var orderA = await CreateDeliveredOrderAsync("user-1");
        var orderB = await CreateDeliveredOrderAsync("user-2");
        var requestA = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(orderA.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(orderA.Items[0].Id, 1) }));
        var requestB = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-2", new CreateReturnRequestRequest(orderB.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(orderB.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(requestA.Value.Id);

        var queue = await _harness.ReturnService.GetPendingQueueAsync(new ReturnRequestQuery { Page = 1, PageSize = 20 });

        queue.TotalCount.Should().Be(1);
        queue.Items.Should().ContainSingle(i => i.Id == requestB.Value.Id);
    }

    [Fact]
    public async Task GetReturnRequestsForOrderAsync_returns_all_requests_newest_first()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var first = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));
        await _harness.ReturnService.RejectAsync(first.Value.Id, "Not eligible.");
        var orderId = (await _harness.OrderService.GetByOrderNumberAsync("user-1", order.OrderNumber)).Value.Id;
        var second = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.WrongItem, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        var requests = await _harness.ReturnService.GetReturnRequestsForOrderAsync(orderId);

        requests.Should().HaveCount(2);
        requests[0].Id.Should().Be(second.Value.Id);
        requests[1].Id.Should().Be(first.Value.Id);
    }

    [Fact]
    public async Task RefundAsync_refunds_the_returned_items_and_restocks_the_original_warehouse()
    {
        var (productId, inventoryItemId) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var order = await CreateDeliveredOrderAsync("user-1", productId);
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(submitted.Value.Id);

        var onHandAfterShip = (await _harness.InventoryService.GetInventoryItemByIdAsync(inventoryItemId)).Value.QuantityOnHand;

        var result = await _harness.ReturnService.RefundAsync(submitted.Value.Id);

        result.IsSuccess.Should().BeTrue();
        var restocked = await _harness.InventoryService.GetInventoryItemByIdAsync(inventoryItemId);
        restocked.Value.QuantityOnHand.Should().Be(onHandAfterShip + 1);

        var orderId = (await _harness.OrderService.GetByOrderNumberAsync("user-1", order.OrderNumber)).Value.Id;
        var requests = await _harness.ReturnService.GetReturnRequestsForOrderAsync(orderId);
        var refunded = requests.Should().ContainSingle().Subject;
        refunded.Status.Should().Be(ReturnRequestStatus.Refunded);
        refunded.RefundedAmount.Should().Be(100m);
        refunded.RefundedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RefundAsync_rejects_a_request_that_is_not_approved()
    {
        var order = await CreateDeliveredOrderAsync("user-1");
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(order.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(order.Items[0].Id, 1) }));

        var result = await _harness.ReturnService.RefundAsync(submitted.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task RefundAsync_returns_not_found_for_an_unknown_request()
    {
        var result = await _harness.ReturnService.RefundAsync(999999);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAwaitingReceiptQueueAsync_only_returns_approved_requests()
    {
        var orderA = await CreateDeliveredOrderAsync("user-1");
        var orderB = await CreateDeliveredOrderAsync("user-2");
        var requestA = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-1", new CreateReturnRequestRequest(orderA.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(orderA.Items[0].Id, 1) }));
        var requestB = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-2", new CreateReturnRequestRequest(orderB.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(orderB.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(requestA.Value.Id);

        var queue = await _harness.ReturnService.GetAwaitingReceiptQueueAsync(new ReturnRequestQuery { Page = 1, PageSize = 20 });

        queue.TotalCount.Should().Be(1);
        queue.Items.Should().ContainSingle(i => i.Id == requestA.Value.Id);
    }

    private async Task<OrderDto> CreateDeliveredOrderAsync(string userId, int? productId = null)
    {
        var order = await CreatePaidOrderAsync(userId, productId);
        await _harness.OrderService.ShipAsync(order.Id, new ShipOrderRequest("UPS", "1Z999AA10123456784"));
        var delivered = await _harness.OrderService.MarkDeliveredAsync(order.Id);
        return delivered.Value;
    }

    private async Task<OrderDto> CreatePaidOrderAsync(string userId, int? productId = null)
    {
        var request = StandardRequest(userId, Guid.NewGuid().ToString("N"));
        if (productId.HasValue)
        {
            request = request with { Items = OneLine(productId.Value, quantity: 1) };
        }

        var result = await _harness.OrderService.CreateOrderAsync(request);
        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        return result.Value;
    }

    private static List<CartItemDto> OneLine(int productId, int quantity) => new()
    {
        new(1, productId, null, "Widget", "widget", null, "SKU-1", null, 100m, null, null, quantity, 100m * quantity,
            ProductStockState.InStock, 20, true, false, null, false),
    };

    private async Task<(int ProductId, int InventoryItemId)> SeedInventoryItemAsync(int quantity, int reorderLevel, bool allowBackorder = false)
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

        var result = await _harness.InventoryService.RecordOpeningStockAsync(
            new RecordOpeningStockRequest(warehouse.Id, product.Id, null, quantity, reorderLevel, reorderLevel * 2, allowBackorder));

        return (product.Id, result.Value.Id);
    }

    private static CreateOrderRequest StandardRequest(string userId, string idempotencyKey) => new(
        userId,
        "customer@example.com",
        idempotencyKey,
        new AddressDto(1, "Home", "Jane Doe", "555-0100", "123 Main St", null, "Springfield", "CA", "90210", "US", true),
        AppliedPromotionId: null,
        new ShippingOptionDto(1, "Standard Shipping", null, 7m, null, null),
        new List<CartItemDto>
        {
            new(1, 1, null, "Widget", "widget", null, "SKU-1", null, 100m, null, null, 1, 100m,
                ProductStockState.InStock, 10, true, false, null, false),
        },
        StandardCalculation(),
        StandardPayment());

    private static CheckoutCalculationResult StandardCalculation() => new(
        Subtotal: 100m, PromotionDiscount: 0m, AppliedCouponCode: null, AppliedPromotionName: null,
        DiscountedSubtotal: 100m, Tax: 10m, TaxRateConfigured: true,
        Shipping: 7m, ShippingRateConfigured: true, GrandTotal: 117m);

    private static ChargeRequest StandardPayment() => new(
        "4242424242424242", "Jane Doe", 12, 2030, "123", Amount: 0m);
}
