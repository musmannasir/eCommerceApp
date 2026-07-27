using System.Net.Http.Json;
using System.Text.Json;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Taxation;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the Cart page's estimated-tax display (Milestone 7.2) over real
/// HTTP against the real SQL Server test database - same reasoning as
/// CartFlowTests/CouponFlowTests. The test app's Store:DefaultTaxCountryCode
/// is "PK" (appsettings.json, not overridden by AuthWebApplicationFactory),
/// so rates are seeded against that country to get a real match.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class EstimatedTaxFlowTests
{
    private readonly AuthTestFixture _fixture;

    public EstimatedTaxFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Adding_a_taxable_item_returns_an_estimated_tax_when_a_rate_is_configured()
    {
        // A per-test-unique TaxCategory keeps this isolated from other tests
        // sharing the same collection-wide database, same reasoning as
        // CouponFlowTests' per-test-unique coupon codes.
        var taxCategory = $"Standard-{Guid.NewGuid():N}";
        var product = await SeedProductAsync(price: 100m, taxCategory);
        await SeedTaxRateAsync("PK", null, taxCategory, 10m);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var response = await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfToken);
        var cart = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.IsSuccessStatusCode.Should().BeTrue();
        cart.GetProperty("estimatedTaxRateConfigured").GetBoolean().Should().BeTrue();
        cart.GetProperty("estimatedTax").GetDecimal().Should().Be(10m);
    }

    [Fact]
    public async Task Adding_an_item_with_no_configured_rate_returns_zero_unconfigured_estimated_tax()
    {
        var taxCategory = $"NeverConfigured-{Guid.NewGuid():N}";
        var product = await SeedProductAsync(price: 100m, taxCategory);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var response = await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfToken);
        var cart = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.IsSuccessStatusCode.Should().BeTrue();
        cart.GetProperty("estimatedTaxRateConfigured").GetBoolean().Should().BeFalse();
        cart.GetProperty("estimatedTax").GetDecimal().Should().Be(0m);
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

    private async Task<Product> SeedProductAsync(decimal price, string taxCategory)
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
            TaxCategory = taxCategory,
            IsTaxable = true,
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    private async Task SeedTaxRateAsync(string countryCode, string? regionCode, string taxCategory, decimal ratePercent)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.TaxRates.Add(new TaxRate
        {
            CountryCode = countryCode,
            RegionCode = regionCode,
            TaxCategory = taxCategory,
            RatePercent = ratePercent,
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();
    }
}
