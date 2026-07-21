using System.Net.Http.Json;
using System.Text.Json;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using ECommerceApp.Web.Controllers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives Wishlist's AJAX endpoints over real HTTP against the real SQL
/// Server test database (Milestone 6.3) - proves the [Authorize] gate
/// actually rejects an anonymous AJAX-style request with 401 (via
/// Program.cs's OnRedirectToLogin override, since a plain 302 would
/// otherwise be silently followed by fetch()), and that toggle/list work
/// end-to-end for a signed-in customer.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class WishlistFlowTests
{
    private readonly AuthTestFixture _fixture;

    public WishlistFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_anonymous_ajax_style_toggle_request_gets_a_401_not_a_followed_redirect()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Wishlist/Toggle")
        {
            Content = JsonContent.Create(new ToggleWishlistRequest(product.Id)),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);

        ((int)response.StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task An_anonymous_visitor_loading_the_wishlist_page_is_redirected_to_login()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Wishlist");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in");
    }

    [Fact]
    public async Task A_signed_in_customer_can_toggle_a_product_onto_and_off_their_wishlist()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        var email = $"wishlist.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Wish", "List");
        var csrfToken = await GetCsrfTokenAsync(client);

        var addResponse = await PostJsonAsync(client, "/Wishlist/Toggle", new ToggleWishlistRequest(product.Id), csrfToken);
        var addResult = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        addResult.GetProperty("isWishlisted").GetBoolean().Should().BeTrue();
        addResult.GetProperty("itemCount").GetInt32().Should().Be(1);

        var pageResponse = await client.GetAsync("/Wishlist");
        var pageBody = await pageResponse.Content.ReadAsStringAsync();
        pageBody.Should().Contain(product.Name);

        var removeResponse = await PostJsonAsync(client, "/Wishlist/Toggle", new ToggleWishlistRequest(product.Id), csrfToken);
        var removeResult = await removeResponse.Content.ReadFromJsonAsync<JsonElement>();
        removeResult.GetProperty("isWishlisted").GetBoolean().Should().BeFalse();
        removeResult.GetProperty("itemCount").GetInt32().Should().Be(0);
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
}
