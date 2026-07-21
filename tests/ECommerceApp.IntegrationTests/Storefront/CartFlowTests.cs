using System.Net.Http.Json;
using System.Text.Json;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives Cart's AJAX endpoints over real HTTP against the real SQL Server
/// test database - proves the guest-cookie round trip actually works through
/// a real request/response cycle (not just a hand-built HttpContext, as the
/// unit tests use), and that CartService's IgnoreQueryFilters()/dictionary
/// projection queries translate against the real provider, not just InMemory.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class CartFlowTests
{
    private readonly AuthTestFixture _fixture;

    public CartFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_guest_can_add_an_item_and_it_persists_across_requests_via_the_cookie()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var addResponse = await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 2), csrfToken);
        var addBody = await addResponse.Content.ReadAsStringAsync();
        addResponse.IsSuccessStatusCode.Should().BeTrue(because: addBody);

        var summaryResponse = await client.GetAsync("/Cart/Summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("itemCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Adding_more_than_available_stock_returns_a_400_with_a_descriptive_message()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, onHand: 2);
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var response = await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 5), csrfToken);
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).Should().Be(400);
        body.Should().Contain("2");
    }

    [Fact]
    public async Task A_request_without_the_csrf_header_is_rejected()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        await GetCsrfTokenAsync(client); // ensures the antiforgery cookie is set, token deliberately not attached below

        var response = await client.PostAsJsonAsync("/Cart/Add", new AddCartItemRequest(product.Id, null, 1));

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Two_separate_clients_guest_carts_are_isolated()
    {
        var product = await SeedProductAsync();
        var clientA = _fixture.Factory.CreateClient();
        var csrfTokenA = await GetCsrfTokenAsync(clientA);
        await PostJsonAsync(clientA, "/Cart/Add", new AddCartItemRequest(product.Id, null, 1), csrfTokenA);

        var clientB = _fixture.Factory.CreateClient();
        var summaryResponse = await clientB.GetAsync("/Cart/Summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<JsonElement>();

        summary.GetProperty("itemCount").GetInt32().Should().Be(0);
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

    private async Task<Product> SeedProductAsync()
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
            CostPrice = 5,
            SellingPrice = 19.99m,
            IsActive = true,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    private async Task SeedInventoryAsync(int productId, int onHand)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        dbContext.InventoryItems.Add(new InventoryItem
        {
            WarehouseId = warehouse.Id,
            ProductId = productId,
            QuantityOnHand = onHand,
            QuantityReserved = 0,
            AllowBackorder = false,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }
}
