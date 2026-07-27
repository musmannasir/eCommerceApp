using System.Net.Http.Json;
using System.Text.Json;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Shipping;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the Cart page's estimated-shipping display (Milestone 7.3) over
/// real HTTP against the real SQL Server test database - same reasoning as
/// EstimatedTaxFlowTests. The test app's Store:DefaultShippingCountryCode is
/// "PK" (appsettings.json, not overridden by AuthWebApplicationFactory), so
/// methods are seeded against that country to get a real match.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class EstimatedShippingFlowTests
{
    private readonly AuthTestFixture _fixture;

    public EstimatedShippingFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Adding_an_item_returns_an_estimated_shipping_cost_when_a_method_is_configured()
    {
        // A per-test-unique method name keeps this isolated from other tests
        // sharing the same collection-wide database, same reasoning as
        // CouponFlowTests' per-test-unique coupon codes.
        var methodName = $"Standard-{Guid.NewGuid():N}";
        var product = await SeedProductAsync(price: 50m, weight: 2m);
        await SeedShippingMethodAsync(methodName, "PK", null, baseRate: 5m, ratePerKg: 1m);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var response = await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfToken);
        var cart = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.IsSuccessStatusCode.Should().BeTrue();
        cart.GetProperty("estimatedShippingRateConfigured").GetBoolean().Should().BeTrue();
        cart.GetProperty("estimatedShipping").GetDecimal().Should().Be(7m); // 5 + 1*2
    }

    // No "unconfigured" counterpart here, unlike EstimatedTaxFlowTests - Tax's
    // lookup key includes TaxCategory, which each test can randomize for
    // isolation; Shipping's key is just (CountryCode, RegionCode), and the
    // real app's default region is a fixed jurisdiction (PK/whole-country)
    // that TestDatabase.ResetAsync only clears once per collection run, not
    // per test - a second test asserting "nothing configured" for that same
    // fixed jurisdiction would be fragile against ordering/accumulated state.
    // The "no method configured" behavior itself is already fully covered in
    // isolation by ShippingServiceTests and CartServiceTests (InMemory DB,
    // fresh per test).

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

    private async Task<Product> SeedProductAsync(decimal price, decimal? weight)
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
            Weight = weight,
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    private async Task SeedShippingMethodAsync(string name, string countryCode, string? regionCode, decimal baseRate, decimal ratePerKg)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.ShippingMethods.Add(new ShippingMethod
        {
            Name = name,
            CountryCode = countryCode,
            RegionCode = regionCode,
            BaseRate = baseRate,
            RatePerKg = ratePerKg,
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();
    }
}
