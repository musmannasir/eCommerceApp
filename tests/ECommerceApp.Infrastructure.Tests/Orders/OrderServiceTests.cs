using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Notifications.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Domain.Notifications;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Orders;

public class OrderServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_an_order_succeeds_and_persists_the_snapshotted_fields()
    {
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N"));

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderNumber.Should().MatchRegex(@"^ORD-\d{6}$");
        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        result.Value.PaymentStatus.Should().Be(nameof(PaymentStatus.Succeeded));
        result.Value.MaskedCardNumber.Should().Be("**** **** **** 4242");
        result.Value.CardBrand.Should().Be("Visa");
        result.Value.DeclineReason.Should().BeNull();
        result.Value.ShippingFullName.Should().Be("Jane Doe");
        result.Value.ShippingCity.Should().Be("Springfield");
        result.Value.Subtotal.Should().Be(100m);
        result.Value.Tax.Should().Be(10m);
        result.Value.GrandTotal.Should().Be(117m);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LineTotal.Should().Be(100m);
    }

    [Fact]
    public async Task Creating_an_order_with_a_declining_test_card_marks_it_PaymentFailed()
    {
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        };

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));
        result.Value.PaymentStatus.Should().Be(nameof(PaymentStatus.Failed));
        result.Value.DeclineReason.Should().Be("Your card was declined.");
        // The order itself is still real and persisted - a declined charge
        // does not prevent the order from existing.
        result.Value.OrderNumber.Should().MatchRegex(@"^ORD-\d{6}$");
    }

    [Fact]
    public async Task A_paid_order_enqueues_an_order_confirmation_outbox_message_atomically_with_the_order()
    {
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N"));

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));

        var message = _harness.DbContext.OutboxMessages.Should().ContainSingle().Subject;
        message.Type.Should().Be(OutboxMessageType.OrderConfirmationEmail);
        message.Status.Should().Be(OutboxMessageStatus.Pending);

        var payload = System.Text.Json.JsonSerializer.Deserialize<OrderConfirmationEmailOutboxPayload>(message.PayloadJson)!;
        payload.ToEmail.Should().Be("customer@example.com");
        payload.Model.OrderNumber.Should().Be(result.Value.OrderNumber);
        payload.Model.GrandTotal.Should().Be(117m);
    }

    [Fact]
    public async Task A_declined_order_does_not_enqueue_an_order_confirmation_outbox_message()
    {
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        };

        await _harness.OrderService.CreateOrderAsync(request);

        _harness.DbContext.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Creating_an_order_with_the_same_idempotency_key_returns_the_original_order()
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var first = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", idempotencyKey));

        // A second submission for the same key, even with different totals,
        // must resolve to the SAME order rather than creating a duplicate or
        // updating the first one - the second submission's own data is never
        // trusted once the first has already been persisted.
        var second = await _harness.OrderService.CreateOrderAsync(
            StandardRequest("user-1", idempotencyKey) with { Calculation = StandardCalculation() with { Subtotal = 999m } });

        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().Be(first.Value.Id);
        second.Value.OrderNumber.Should().Be(first.Value.OrderNumber);
        second.Value.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task Different_users_can_use_the_same_idempotency_key_without_colliding()
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var forUser1 = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", idempotencyKey));
        var forUser2 = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-2", idempotencyKey));

        forUser1.IsSuccess.Should().BeTrue();
        forUser2.IsSuccess.Should().BeTrue();
        forUser2.Value.Id.Should().NotBe(forUser1.Value.Id);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_returns_not_found_for_an_unknown_key()
    {
        var result = await _harness.OrderService.GetByIdempotencyKeyAsync("user-1", "does-not-exist");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetByOrderNumberAsync_does_not_return_another_users_order()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.GetByOrderNumberAsync("user-2", created.Value.OrderNumber);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetByOrderNumberAsync_returns_the_order_for_its_owner()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.GetByOrderNumberAsync("user-1", created.Value.OrderNumber);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(created.Value.Id);
    }

    [Fact]
    public async Task Creating_a_paid_order_reserves_stock_for_a_tracked_product()
    {
        var (productId, itemId) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with { Items = OneLine(productId, quantity: 5) };

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(5);
    }

    [Fact]
    public async Task Creating_an_order_releases_its_reservation_when_payment_fails()
    {
        var (productId, itemId) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Items = OneLine(productId, quantity: 5),
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        };

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Creating_an_order_with_insufficient_stock_fails_reservation_without_charging_the_card()
    {
        var (productId, itemId) = await SeedInventoryItemAsync(quantity: 2, reorderLevel: 1, allowBackorder: false);
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with { Items = OneLine(productId, quantity: 5) };

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.StockReservationFailed));
        result.Value.StockIssueMessage.Should().Contain("Widget");
        result.Value.MaskedCardNumber.Should().BeNull();
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Creating_an_order_for_an_untracked_product_skips_reservation_and_still_charges()
    {
        // No InventoryItem row exists at all for product id 1 in a fresh
        // harness - an untracked product is never blocked on reservation.
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N"));

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        result.Value.StockIssueMessage.Should().BeNull();
    }

    [Fact]
    public async Task Creating_a_multiline_order_releases_an_earlier_reservation_when_a_later_line_fails()
    {
        var (productId1, itemId1) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var (productId2, itemId2) = await SeedInventoryItemAsync(quantity: 2, reorderLevel: 1, allowBackorder: false);
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Items = new List<CartItemDto>
            {
                new(1, productId1, null, "Widget", "widget", null, "SKU-1", null, 100m, null, null, 5, 500m,
                    ProductStockState.InStock, 20, true, false, null, false),
                new(2, productId2, null, "Gadget", "gadget", null, "SKU-2", null, 50m, null, null, 5, 250m,
                    ProductStockState.InStock, 2, true, false, null, false),
            },
        };

        var result = await _harness.OrderService.CreateOrderAsync(request);

        result.Value.Status.Should().Be(nameof(OrderStatus.StockReservationFailed));
        var item1 = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId1);
        item1.Value.QuantityReserved.Should().Be(0);
        var item2 = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId2);
        item2.Value.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_returns_orders_newest_first()
    {
        var first = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        var second = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.GetPagedAsync(new OrderQuery());

        result.Value.Items[0].Id.Should().Be(second.Value.Id);
        result.Value.Items[1].Id.Should().Be(first.Value.Id);
    }

    [Fact]
    public async Task GetPagedAsync_search_matches_order_number_or_customer_name()
    {
        var request = StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Address = StandardRequest("user-1", "unused").Address with { FullName = "Alice Anderson" },
        };
        var created = await _harness.OrderService.CreateOrderAsync(request);

        var byName = await _harness.OrderService.GetPagedAsync(new OrderQuery { Search = "Alice" });
        var byOrderNumber = await _harness.OrderService.GetPagedAsync(new OrderQuery { Search = created.Value.OrderNumber });
        var byUnrelatedTerm = await _harness.OrderService.GetPagedAsync(new OrderQuery { Search = "no-such-customer" });

        byName.Value.Items.Should().Contain(i => i.Id == created.Value.Id);
        byOrderNumber.Value.Items.Should().ContainSingle(i => i.Id == created.Value.Id);
        byUnrelatedTerm.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_status()
    {
        var paid = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        var (productId, _) = await SeedInventoryItemAsync(quantity: 1, reorderLevel: 1, allowBackorder: false);
        var stockFailed = await _harness.OrderService.CreateOrderAsync(
            StandardRequest("user-1", Guid.NewGuid().ToString("N")) with { Items = OneLine(productId, quantity: 5) });

        var paidOnly = await _harness.OrderService.GetPagedAsync(new OrderQuery { Status = nameof(OrderStatus.Paid) });
        var stockFailedOnly = await _harness.OrderService.GetPagedAsync(new OrderQuery { Status = nameof(OrderStatus.StockReservationFailed) });

        paidOnly.Value.Items.Should().Contain(i => i.Id == paid.Value.Id).And.NotContain(i => i.Id == stockFailed.Value.Id);
        stockFailedOnly.Value.Items.Should().Contain(i => i.Id == stockFailed.Value.Id).And.NotContain(i => i.Id == paid.Value.Id);
    }

    [Fact]
    public async Task GetPagedAsync_paginates_correctly()
    {
        for (var i = 0; i < 3; i++)
        {
            await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        }

        var firstPage = await _harness.OrderService.GetPagedAsync(new OrderQuery { Page = 1, PageSize = 2 });
        var secondPage = await _harness.OrderService.GetPagedAsync(new OrderQuery { Page = 2, PageSize = 2 });

        firstPage.Value.Items.Should().HaveCount(2);
        firstPage.Value.TotalCount.Should().Be(3);
        secondPage.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDashboardAsync_only_includes_the_given_users_orders()
    {
        await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        await _harness.OrderService.CreateOrderAsync(StandardRequest("user-2", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.GetDashboardAsync("user-1", page: 1, pageSize: 10);

        result.Value.TotalOrders.Should().Be(1);
        result.Value.Orders.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetDashboardAsync_only_counts_total_spent_from_successfully_charged_orders()
    {
        var paid = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        paid.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        var failed = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        });
        failed.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));

        var result = await _harness.OrderService.GetDashboardAsync("user-1", page: 1, pageSize: 10);

        result.Value.TotalOrders.Should().Be(2);
        result.Value.TotalSpent.Should().Be(paid.Value.GrandTotal);
    }

    [Fact]
    public async Task GetDashboardAsync_paginates_correctly()
    {
        for (var i = 0; i < 3; i++)
        {
            await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        }

        var firstPage = await _harness.OrderService.GetDashboardAsync("user-1", page: 1, pageSize: 2);
        var secondPage = await _harness.OrderService.GetDashboardAsync("user-1", page: 2, pageSize: 2);

        firstPage.Value.Orders.Items.Should().HaveCount(2);
        firstPage.Value.TotalOrders.Should().Be(3);
        secondPage.Value.Orders.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_returns_an_order_regardless_of_which_user_placed_it()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.GetByIdAsync(created.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderNumber.Should().Be(created.Value.OrderNumber);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_for_an_unknown_id()
    {
        var result = await _harness.OrderService.GetByIdAsync(999999);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CancelAsync_releases_the_reservation_and_marks_a_paid_order_cancelled()
    {
        var (productId, itemId) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var created = await _harness.OrderService.CreateOrderAsync(
            StandardRequest("user-1", Guid.NewGuid().ToString("N")) with { Items = OneLine(productId, quantity: 5) });
        created.Value.Status.Should().Be(nameof(OrderStatus.Paid));

        var result = await _harness.OrderService.CancelAsync(created.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.Cancelled));
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task CancelAsync_rejects_an_order_that_is_not_paid()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        });
        created.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));

        var result = await _harness.OrderService.CancelAsync(created.Value.Id);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CancelOwnOrderAsync_releases_the_reservation_and_marks_a_paid_order_cancelled()
    {
        var (productId, itemId) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var created = await _harness.OrderService.CreateOrderAsync(
            StandardRequest("user-1", Guid.NewGuid().ToString("N")) with { Items = OneLine(productId, quantity: 5) });
        created.Value.Status.Should().Be(nameof(OrderStatus.Paid));

        var result = await _harness.OrderService.CancelOwnOrderAsync("user-1", created.Value.OrderNumber);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.Cancelled));
        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task CancelOwnOrderAsync_rejects_an_order_that_is_not_paid()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        });
        created.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));

        var result = await _harness.OrderService.CancelOwnOrderAsync("user-1", created.Value.OrderNumber);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CancelOwnOrderAsync_returns_not_found_for_another_users_order()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        created.Value.Status.Should().Be(nameof(OrderStatus.Paid));

        var result = await _harness.OrderService.CancelOwnOrderAsync("user-2", created.Value.OrderNumber);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAdminNotesAsync_saves_and_returns_the_note()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.UpdateAdminNotesAsync(created.Value.Id, "Called customer to confirm address.");

        result.IsSuccess.Should().BeTrue();
        result.Value.AdminNotes.Should().Be("Called customer to confirm address.");
    }

    [Fact]
    public async Task ShipAsync_consumes_the_reservation_and_marks_a_paid_order_shipped()
    {
        var (productId, itemId) = await SeedInventoryItemAsync(quantity: 20, reorderLevel: 2);
        var created = await _harness.OrderService.CreateOrderAsync(
            StandardRequest("user-1", Guid.NewGuid().ToString("N")) with { Items = OneLine(productId, quantity: 5) });
        created.Value.Status.Should().Be(nameof(OrderStatus.Paid));

        var result = await _harness.OrderService.ShipAsync(created.Value.Id, new ShipOrderRequest("UPS", "1Z999AA10123456784"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.Shipped));
        result.Value.Carrier.Should().Be("UPS");
        result.Value.TrackingNumber.Should().Be("1Z999AA10123456784");
        result.Value.ShippedAtUtc.Should().NotBeNull();

        var item = await _harness.InventoryService.GetInventoryItemByIdAsync(itemId);
        item.Value.QuantityReserved.Should().Be(0);
        item.Value.QuantityOnHand.Should().Be(15);
    }

    [Fact]
    public async Task ShipAsync_rejects_an_order_that_is_not_paid()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        });
        created.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));

        var result = await _harness.OrderService.ShipAsync(created.Value.Id, new ShipOrderRequest("UPS", "1Z999AA10123456784"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MarkDeliveredAsync_marks_a_shipped_order_delivered()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        await _harness.OrderService.ShipAsync(created.Value.Id, new ShipOrderRequest("UPS", "1Z999AA10123456784"));

        var result = await _harness.OrderService.MarkDeliveredAsync(created.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.Delivered));
        result.Value.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkDeliveredAsync_rejects_an_order_that_has_not_shipped()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));

        var result = await _harness.OrderService.MarkDeliveredAsync(created.Value.Id);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CancelAsync_rejects_an_order_that_has_already_shipped()
    {
        var created = await _harness.OrderService.CreateOrderAsync(StandardRequest("user-1", Guid.NewGuid().ToString("N")));
        await _harness.OrderService.ShipAsync(created.Value.Id, new ShipOrderRequest("UPS", "1Z999AA10123456784"));

        var result = await _harness.OrderService.CancelAsync(created.Value.Id);

        result.IsFailure.Should().BeTrue();
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

    // The well-known Stripe test card that always succeeds - see
    // SimulatedPaymentGateway's own remarks for why this specific number.
    private static ChargeRequest StandardPayment() => new(
        "4242424242424242", "Jane Doe", 12, 2030, "123", Amount: 0m);
}
