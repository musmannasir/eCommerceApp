using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Orders;
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
        result.Value.Status.Should().Be(nameof(OrderStatus.Pending));
        result.Value.ShippingFullName.Should().Be("Jane Doe");
        result.Value.ShippingCity.Should().Be("Springfield");
        result.Value.Subtotal.Should().Be(100m);
        result.Value.Tax.Should().Be(10m);
        result.Value.GrandTotal.Should().Be(117m);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LineTotal.Should().Be(100m);
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
        StandardCalculation());

    private static CheckoutCalculationResult StandardCalculation() => new(
        Subtotal: 100m, PromotionDiscount: 0m, AppliedCouponCode: null, AppliedPromotionName: null,
        DiscountedSubtotal: 100m, Tax: 10m, TaxRateConfigured: true,
        Shipping: 7m, ShippingRateConfigured: true, GrandTotal: 117m);
}
