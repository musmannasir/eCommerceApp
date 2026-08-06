using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Reporting.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Reporting;

public class ReportingServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GetTopSellingProductsAsync_groups_by_product_and_orders_by_quantity_descending()
    {
        var productA = await SeedProductAsync();
        var productB = await SeedProductAsync();

        await CreatePaidOrderAsync("user-1", productA, quantity: 3, unitPrice: 10m); // A: qty 3, revenue 30
        await CreatePaidOrderAsync("user-2", productA, quantity: 2, unitPrice: 10m); // A: qty 2, revenue 20 -> total qty 5, revenue 50
        await CreatePaidOrderAsync("user-3", productB, quantity: 1, unitPrice: 100m); // B: qty 1, revenue 100 (higher revenue, lower quantity)

        var result = await _harness.ReportingService.GetTopSellingProductsAsync(new TopSellingProductsQuery());

        result.Products.Should().HaveCount(2);
        result.Products[0].ProductId.Should().Be(productA);
        result.Products[0].QuantitySold.Should().Be(5);
        result.Products[0].Revenue.Should().Be(50m);
        result.Products[1].ProductId.Should().Be(productB);
        result.Products[1].QuantitySold.Should().Be(1);
        result.Products[1].Revenue.Should().Be(100m);
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_excludes_orders_that_were_never_successfully_charged()
    {
        var product = await SeedProductAsync();
        await CreateDeclinedOrderAsync("user-1", product, quantity: 5, unitPrice: 10m);

        var result = await _harness.ReportingService.GetTopSellingProductsAsync(new TopSellingProductsQuery());

        result.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_respects_the_take_limit()
    {
        var productA = await SeedProductAsync();
        var productB = await SeedProductAsync();
        var productC = await SeedProductAsync();
        await CreatePaidOrderAsync("user-1", productA, quantity: 3, unitPrice: 10m);
        await CreatePaidOrderAsync("user-2", productB, quantity: 2, unitPrice: 10m);
        await CreatePaidOrderAsync("user-3", productC, quantity: 1, unitPrice: 10m);

        var result = await _harness.ReportingService.GetTopSellingProductsAsync(new TopSellingProductsQuery { Take = 2 });

        result.Products.Should().HaveCount(2);
        result.Products[0].ProductId.Should().Be(productA);
        result.Products[1].ProductId.Should().Be(productB);
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_defaults_to_the_30_days_ending_today()
    {
        var today = _harness.Clock.UtcNow.Date;
        var product = await SeedProductAsync();
        await CreatePaidOrderAsync("user-1", product, quantity: 1, unitPrice: 10m);

        var result = await _harness.ReportingService.GetTopSellingProductsAsync(new TopSellingProductsQuery());

        result.From.Should().Be(today.AddDays(-29));
        result.To.Should().Be(today);
        result.Products.Should().ContainSingle(p => p.ProductId == product);
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_ignores_orders_outside_the_requested_range()
    {
        var product = await SeedProductAsync();
        var startDate = _harness.Clock.UtcNow.Date;
        await CreatePaidOrderAsync("user-1", product, quantity: 1, unitPrice: 10m);

        _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddDays(10);
        var result = await _harness.ReportingService.GetTopSellingProductsAsync(
            new TopSellingProductsQuery { From = startDate.AddDays(1), To = startDate.AddDays(5) });

        result.Products.Should().BeEmpty();
    }

    private async Task<int> SeedProductAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = $"Product {Guid.NewGuid():N}",
            Slug = $"product-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = true,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();

        return product.Id;
    }

    private async Task<OrderDto> CreatePaidOrderAsync(string userId, int productId, int quantity, decimal unitPrice)
    {
        var result = await _harness.OrderService.CreateOrderAsync(
            StandardRequest(userId, Guid.NewGuid().ToString("N"), productId, quantity, unitPrice));
        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        return result.Value;
    }

    private async Task<OrderDto> CreateDeclinedOrderAsync(string userId, int productId, int quantity, decimal unitPrice)
    {
        var request = StandardRequest(userId, Guid.NewGuid().ToString("N"), productId, quantity, unitPrice) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        };
        var result = await _harness.OrderService.CreateOrderAsync(request);
        result.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));
        return result.Value;
    }

    private static CreateOrderRequest StandardRequest(string userId, string idempotencyKey, int productId, int quantity, decimal unitPrice) => new(
        userId,
        "customer@example.com",
        idempotencyKey,
        new AddressDto(1, "Home", "Jane Doe", "555-0100", "123 Main St", null, "Springfield", "CA", "90210", "US", true),
        AppliedPromotionId: null,
        new ShippingOptionDto(1, "Standard Shipping", null, 0m, null, null),
        new List<CartItemDto>
        {
            new(1, productId, null, "Widget", "widget", null, "SKU-1", null, unitPrice, null, null, quantity, unitPrice * quantity,
                ProductStockState.InStock, 10, true, false, null, false),
        },
        new CheckoutCalculationResult(
            Subtotal: unitPrice * quantity, PromotionDiscount: 0m, AppliedCouponCode: null, AppliedPromotionName: null,
            DiscountedSubtotal: unitPrice * quantity, Tax: 0m, TaxRateConfigured: false,
            Shipping: 0m, ShippingRateConfigured: false, GrandTotal: unitPrice * quantity),
        StandardPayment());

    private static ChargeRequest StandardPayment() => new(
        "4242424242424242", "Jane Doe", 12, 2030, "123", Amount: 0m);
}
