using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Inventory;
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
