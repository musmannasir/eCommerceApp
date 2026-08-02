using System.Text.RegularExpressions;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the product detail page's review submission and rating summary
/// (Milestone 12.1) over real HTTP - an anonymous visitor is prompted to log
/// in rather than shown a form, a signed-in customer's review shows up
/// immediately with the correct rating summary, a customer who actually
/// bought the product gets the Verified Purchase badge and one who didn't
/// does not, and a second submission for the same product is rejected.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ReviewFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ReviewFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_anonymous_visitor_sees_a_login_prompt_instead_of_the_review_form()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();

        var body = await client.GetStringAsync($"/Product/{product.Slug}");

        body.Should().Contain("Log in").And.Contain("to write a review");
    }

    [Fact]
    public async Task A_signed_in_customer_can_submit_a_review_and_sees_it_with_the_updated_rating_summary()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        var email = $"reviewer.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Review", "Er");

        var body = await SubmitReviewAsync(client, product.Slug, product.Id, rating: 4, title: "Pretty good", body: "Does what it says.");

        body.Should().Contain("Thanks - your review has been posted.");
        body.Should().Contain("Pretty good").And.Contain("Does what it says.");
        body.Should().Contain("4.0"); // average rating with a single 4-star review
        body.Should().NotContain("Verified Purchase");
    }

    [Fact]
    public async Task A_customer_who_purchased_the_product_gets_the_verified_purchase_badge()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "RI");

        var client = _fixture.Factory.CreateClient();
        var email = $"verified.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Verified", "Buyer");
        var addressId = await CreateAddressAsync(client, "US", "RI");
        await AddToCartAsync(client, product.Id);
        await PlaceOrderAsync(client, addressId, "4242424242424242");

        var body = await SubmitReviewAsync(client, product.Slug, product.Id, rating: 5, title: null, body: "Bought it and loved it.");

        body.Should().Contain("Verified Purchase");
    }

    [Fact]
    public async Task Submitting_a_second_review_for_the_same_product_is_rejected()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        var email = $"duplicate.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Duplicate", "Reviewer");

        await SubmitReviewAsync(client, product.Slug, product.Id, rating: 3, title: null, body: "First review.");
        var body = await SubmitReviewAsync(client, product.Slug, product.Id, rating: 2, title: null, body: "Trying again.");

        body.Should().Contain("You have already reviewed this product.");
        body.Should().Contain("First review.").And.NotContain("Trying again.");
    }

    /// <summary>
    /// Returns the redirected-to Details page's body directly - the client's
    /// default AllowAutoRedirect already follows the POST's redirect and
    /// consumes TempData while rendering it, so a separate GET afterward
    /// would find the message already cleared.
    /// </summary>
    private static async Task<string> SubmitReviewAsync(HttpClient client, string slug, int productId, int rating, string? title, string body)
    {
        var detailsHtml = await client.GetStringAsync($"/Product/{slug}");
        var token = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);

        var formValues = new Dictionary<string, string>
        {
            ["ProductId"] = productId.ToString(),
            ["Rating"] = rating.ToString(),
            ["Body"] = body,
            ["__RequestVerificationToken"] = token,
        };
        if (title is not null)
        {
            formValues["Title"] = title;
        }

        var response = await client.PostAsync($"/Product/{slug}/Review", new FormUrlEncodedContent(formValues));
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task AddToCartAsync(HttpClient client, int productId)
    {
        var homeHtml = await client.GetStringAsync("/");
        var csrfToken = HtmlHelpers.ExtractMetaCsrfToken(homeHtml);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/Add")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { ProductId = productId, ProductVariantId = (int?)null, Quantity = 1 }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> CreateAddressAsync(HttpClient client, string countryCode, string regionCode)
    {
        var createPageResponse = await client.GetAsync("/Addresses/Create");
        var createPageHtml = await createPageResponse.Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(createPageHtml);

        var formValues = new Dictionary<string, string>
        {
            ["Label"] = "Home",
            ["FullName"] = "Review Tester",
            ["Phone"] = "555-0100",
            ["Line1"] = "123 Main St",
            ["City"] = "Springfield",
            ["RegionCode"] = regionCode,
            ["PostalCode"] = "90210",
            ["CountryCode"] = countryCode,
            ["__RequestVerificationToken"] = token,
        };

        await client.PostAsync("/Addresses/Create", new FormUrlEncodedContent(formValues));

        var indexHtml = await client.GetStringAsync("/Addresses");
        var match = Regex.Match(indexHtml, "/Addresses/Edit/(\\d+)");
        return int.Parse(match.Groups[1].Value);
    }

    private static async Task<string> PlaceOrderAsync(HttpClient client, int addressId, string cardNumber)
    {
        var indexPageHtml = await client.GetStringAsync("/Checkout");
        var indexToken = HtmlHelpers.ExtractAntiForgeryToken(indexPageHtml);
        var toShippingResponse = await client.PostAsync("/Checkout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["addressId"] = addressId.ToString(), ["__RequestVerificationToken"] = indexToken }));
        var shippingPageHtml = await toShippingResponse.Content.ReadAsStringAsync();
        var shippingMethodId = int.Parse(Regex.Match(shippingPageHtml, "name=\"shippingMethodId\"[^>]*value=\"(\\d+)\"").Groups[1].Value);

        var shippingToken = HtmlHelpers.ExtractAntiForgeryToken(shippingPageHtml);
        var toReviewResponse = await client.PostAsync("/Checkout/Shipping", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["addressId"] = addressId.ToString(),
                ["shippingMethodId"] = shippingMethodId.ToString(),
                ["__RequestVerificationToken"] = shippingToken,
            }));
        var reviewPageHtml = await toReviewResponse.Content.ReadAsStringAsync();
        var reviewAddressId = int.Parse(Regex.Match(reviewPageHtml, "name=\"addressId\" value=\"(\\d+)\"").Groups[1].Value);
        var idempotencyKey = Regex.Match(reviewPageHtml, "name=\"idempotencyKey\" value=\"([^\"]+)\"").Groups[1].Value;

        var placeToken = HtmlHelpers.ExtractAntiForgeryToken(reviewPageHtml);
        var placeOrderResponse = await client.PostAsync("/Checkout/PlaceOrder", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["addressId"] = reviewAddressId.ToString(),
            ["shippingMethodId"] = shippingMethodId.ToString(),
            ["idempotencyKey"] = idempotencyKey,
            ["cardNumber"] = cardNumber,
            ["cardholderName"] = "Review Tester",
            ["expiryMonth"] = "12",
            ["expiryYear"] = "2030",
            ["cvv"] = "123",
            ["__RequestVerificationToken"] = placeToken,
        }));

        return placeOrderResponse.RequestMessage!.RequestUri!.AbsolutePath.Split('/').Last();
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
            CostPrice = 50m,
            SellingPrice = 100m,
            IsActive = true,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    private async Task SeedShippingMethodAsync(string countryCode, string regionCode)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.ShippingMethods.Add(new Domain.Shipping.ShippingMethod
        {
            Name = $"Standard Shipping {Guid.NewGuid():N}",
            CountryCode = countryCode,
            RegionCode = regionCode,
            BaseRate = 5m,
            RatePerKg = 0m,
            DisplayOrder = 0,
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();
    }
}
