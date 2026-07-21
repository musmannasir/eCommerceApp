using System.Net.Http.Json;
using System.Text.Json;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the whole guest-to-user cart merge flow (Milestone 6.2) over real
/// HTTP against the real SQL Server test database: register while carrying a
/// guest cart (the "user has no cart yet" fast path), then log back in with a
/// second, different guest cart (the "both exist" line-by-line merge path) -
/// both through the actual AccountController login/register actions, not a
/// hand-built HttpContext.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class CartMergeFlowTests
{
    private readonly AuthTestFixture _fixture;

    public CartMergeFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Registering_with_a_guest_cart_folds_it_into_the_new_account()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(product.Id, null, 2), csrfToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var registerResponse = await client.RegisterViaFormAsync($"merge-{suffix}@example.com", "Str0ng!Passw0rd", "Merge", "Test");
        registerResponse.IsSuccessStatusCode.Should().BeTrue();

        var summary = await (await client.GetAsync("/Cart/Summary")).Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("itemCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Logging_in_merges_a_second_guest_cart_into_the_existing_account_cart()
    {
        var productA = await SeedProductAsync(name: "First");
        var productB = await SeedProductAsync(name: "Second");
        var client = _fixture.Factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"merge-existing-{suffix}@example.com";
        const string password = "Str0ng!Passw0rd";

        var firstCsrfToken = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(productA.Id, null, 2), firstCsrfToken);
        (await client.RegisterViaFormAsync(email, password, "Merge", "Existing")).IsSuccessStatusCode.Should().BeTrue();

        await LogoutAsync(client);

        var secondCsrfToken = await GetCsrfTokenAsync(client);
        await PostJsonAsync(client, "/Cart/Add", new AddCartItemRequest(productB.Id, null, 1), secondCsrfToken);
        (await client.LoginViaFormAsync(email, password)).IsSuccessStatusCode.Should().BeTrue();

        var summary = await (await client.GetAsync("/Cart/Summary")).Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("itemCount").GetInt32().Should().Be(3);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        return HtmlHelpers.ExtractMetaCsrfToken(html);
    }

    private static async Task LogoutAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        var token = HtmlHelpers.ExtractAntiForgeryToken(html);
        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private static Task<HttpResponseMessage> PostJsonAsync<T>(HttpClient client, string url, T body, string csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return client.SendAsync(request);
    }

    private async Task<Product> SeedProductAsync(string name = "Widget")
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = new Category { Name = $"Category {suffix}", Slug = $"cat-{suffix}", DisplayOrder = 0, IsActive = true };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = $"{name} {suffix}",
            Slug = $"{name.ToLowerInvariant()}-{suffix}",
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
}
