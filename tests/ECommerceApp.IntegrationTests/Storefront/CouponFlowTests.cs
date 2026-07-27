using System.Net.Http.Json;
using System.Text.Json;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Marketing;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using ECommerceApp.Web.Controllers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the Cart page's coupon apply/remove AJAX endpoints (Milestone 7.1)
/// over real HTTP against the real SQL Server test database - same reasoning
/// as CartFlowTests, proving PromotionService's queries translate against the
/// real provider and the guest-cookie cart round-trips correctly alongside
/// an applied coupon.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class CouponFlowTests
{
    private readonly AuthTestFixture _fixture;

    public CouponFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Applying_a_valid_coupon_updates_the_cart_total()
    {
        var product = await SeedProductAsync(price: 100m);
        var couponCode = await SeedPromotionAsync(10m);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfToken);

        var response = await PostJsonAsync(client, "/Cart/ApplyCoupon", new ApplyCouponRequest(couponCode), csrfToken);
        var cart = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.IsSuccessStatusCode.Should().BeTrue();
        cart.GetProperty("appliedCouponCode").GetString().Should().Be(couponCode);
        cart.GetProperty("promotionDiscount").GetDecimal().Should().Be(10m);
        cart.GetProperty("total").GetDecimal().Should().Be(90m);
    }

    [Fact]
    public async Task Applying_an_unknown_coupon_returns_a_404()
    {
        var product = await SeedProductAsync(price: 100m);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfToken);

        var response = await PostJsonAsync(client, "/Cart/ApplyCoupon", new ApplyCouponRequest("NOPE"), csrfToken);

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Removing_a_coupon_clears_the_discount()
    {
        var product = await SeedProductAsync(price: 100m);
        var couponCode = await SeedPromotionAsync(10m);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfToken);
        await PostJsonAsync(client, "/Cart/ApplyCoupon", new ApplyCouponRequest(couponCode), csrfToken);

        var response = await PostJsonAsync<object?>(client, "/Cart/RemoveCoupon", null, csrfToken);
        var cart = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.IsSuccessStatusCode.Should().BeTrue();
        cart.GetProperty("appliedCouponCode").ValueKind.Should().Be(JsonValueKind.Null);
        cart.GetProperty("promotionDiscount").GetDecimal().Should().Be(0m);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        return HtmlHelpers.ExtractMetaCsrfToken(html);
    }

    private static Task<HttpResponseMessage> PostJsonAsync<T>(HttpClient client, string url, T body, string csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return client.SendAsync(request);
    }

    private async Task<Product> SeedProductAsync(decimal price)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = new Category { Name = $"Category {suffix}", Slug = $"cat-{suffix}", DisplayOrder = 0, IsActive = true };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = $"Widget {suffix}",
            Slug = $"widget-{suffix}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{suffix}",
            CostPrice = price / 2,
            SellingPrice = price,
            IsActive = true,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    private async Task<string> SeedPromotionAsync(decimal percentageDiscount)
    {
        var couponCode = $"SAVE{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Promotions.Add(new Promotion
        {
            Name = "Test promotion",
            CouponCode = couponCode,
            DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = percentageDiscount,
            ScopeType = PromotionScopeType.EntireOrder,
            StartsAtUtc = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();

        return couponCode;
    }
}
