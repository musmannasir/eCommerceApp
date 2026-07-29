using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the Checkout flow's classic MVC forms over real HTTP against the
/// real SQL Server test database (Milestone 8.2) - the three-step
/// address/shipping/review flow, its guard rails (empty cart, no saved
/// addresses), and that it computes real destination-based totals via
/// ICheckoutCalculationService.CalculateAsync rather than the Cart page's
/// store-default-jurisdiction estimate.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class CheckoutFlowTests
{
    private readonly AuthTestFixture _fixture;

    public CheckoutFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_anonymous_visitor_loading_checkout_is_redirected_to_login()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Checkout");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in");
    }

    [Fact]
    public async Task A_signed_in_customer_with_an_empty_cart_is_redirected_to_the_cart_page()
    {
        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"empty.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Empty", "Cart");

        var response = await client.GetAsync("/Checkout");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Your Cart");
    }

    [Fact]
    public async Task A_customer_with_items_but_no_saved_addresses_is_redirected_to_add_one()
    {
        var product = await SeedProductAsync();
        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"noaddr.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "No", "Address");
        await AddToCartAsync(client, product.Id);

        var response = await client.GetAsync("/Checkout");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New address");
    }

    [Fact]
    public async Task The_full_flow_computes_real_destination_based_totals_and_lets_the_customer_review_the_order()
    {
        var product = await SeedProductAsync(price: 100m, weight: 2m);
        await SeedTaxRateAsync("US", "CA", "Standard", 10m);
        await SeedShippingMethodAsync("US", "CA", baseRate: 5m, ratePerKg: 1m);

        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"checkout.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Check", "Out");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "CA");

        var indexPageHtml = await client.GetStringAsync("/Checkout");
        indexPageHtml.Should().Contain("Springfield");

        // CreateClient() follows redirects automatically, so each POST's
        // response body is already the next step's rendered page.
        var indexToken = HtmlHelpers.ExtractAntiForgeryToken(indexPageHtml);
        var toShippingResponse = await client.PostAsync("/Checkout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["addressId"] = addressId.ToString(), ["__RequestVerificationToken"] = indexToken }));
        var shippingPageHtml = await toShippingResponse.Content.ReadAsStringAsync();
        shippingPageHtml.Should().Contain("Standard Shipping");
        var shippingMethodId = ExtractFirstShippingMethodId(shippingPageHtml);

        var shippingToken = HtmlHelpers.ExtractAntiForgeryToken(shippingPageHtml);
        var toReviewResponse = await client.PostAsync("/Checkout/Shipping", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["addressId"] = addressId.ToString(),
                ["shippingMethodId"] = shippingMethodId.ToString(),
                ["__RequestVerificationToken"] = shippingToken,
            }));
        var reviewPageHtml = await toReviewResponse.Content.ReadAsStringAsync();

        // Subtotal 100, tax 10% of 100 = 10.00, shipping 5 + 1*2 = 7.00, total 117.00.
        reviewPageHtml.Should().Contain("10.00");
        reviewPageHtml.Should().Contain("7.00");
        reviewPageHtml.Should().Contain("117.00");
        reviewPageHtml.Should().Contain("Place order");
    }

    [Fact]
    public async Task A_customer_cannot_use_another_customers_address_id_in_the_checkout_flow()
    {
        var product = await SeedProductAsync();
        var ownerClient = _fixture.Factory.CreateClient();
        await ownerClient.RegisterViaFormAsync($"owner.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Owner", "One");
        var addressId = await CreateAddressAsync(ownerClient, "US", "CA");

        var otherClient = _fixture.Factory.CreateClient();
        await otherClient.RegisterViaFormAsync($"other.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Other", "Two");
        await AddToCartAsync(otherClient, product.Id);
        await CreateAddressAsync(otherClient, "US", "NY", city: "Gotham");

        var response = await otherClient.GetAsync($"/Checkout/Shipping?addressId={addressId}");
        var body = await response.Content.ReadAsStringAsync();

        // Redirected back to address selection (their own addresses, "Gotham")
        // rather than showing the other customer's address ("Springfield").
        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().NotContain("Springfield");
        body.Should().Contain("Gotham");
        body.Should().Contain("Step 1 of 3");
    }

    [Fact]
    public async Task A_customer_whose_cart_now_exceeds_available_stock_is_redirected_to_the_cart_page()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, onHand: 5, allowBackorder: false);

        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"stock.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Stock", "Issue");
        await AddToCartAsync(client, product.Id);

        // Stock depletes after the item was already added to the cart.
        await SetInventoryOnHandAsync(product.Id, onHand: 0);

        var response = await client.GetAsync("/Checkout");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Your Cart");
        body.Should().Contain("exceed available stock");
    }

    [Fact]
    public async Task Placing_the_order_with_insufficient_stock_redirects_to_the_cart_page_instead_of_confirming()
    {
        var product = await SeedProductAsync(price: 100m);
        // A distinct region (FL) from other tests in this collection - the
        // Shipping page renders ALL ShippingMethod rows configured for its
        // jurisdiction, and multiple tests seeding the same region within
        // this shared-DB collection run would let one test's "first shipping
        // method in the page" helper pick up a different test's row.
        await SeedShippingMethodAsync("US", "FL", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 5, allowBackorder: false);

        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"placefail.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Place", "Fail");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "FL");

        var (reviewPageHtml, _) = await ReachReviewAsync(client, addressId);
        var (reviewAddressId, shippingMethodId, idempotencyKey) = ExtractReviewFormValues(reviewPageHtml);

        // Stock depletes after Review was rendered but before the customer submits.
        await SetInventoryOnHandAsync(product.Id, onHand: 0);

        var placeOrderResponse = await PostPlaceOrderAsync(client, reviewPageHtml, reviewAddressId, shippingMethodId, idempotencyKey);
        var body = await placeOrderResponse.Content.ReadAsStringAsync();

        body.Should().Contain("Your Cart");
        body.Should().Contain("exceed available stock");
    }

    [Fact]
    public async Task Placing_the_order_successfully_shows_a_confirmation_with_the_reviewed_totals()
    {
        var product = await SeedProductAsync(price: 100m, weight: 2m);
        // A distinct jurisdiction (TX, not CA) from the pre-existing
        // full-flow test above - both seed a real, non-Guid-suffixed
        // TaxRate for a "Standard" category, and TaxRate's uniqueness key
        // (CountryCode, RegionCode, TaxCategory) has no Name field to
        // de-duplicate by within a shared-DB test run.
        await SeedTaxRateAsync("US", "TX", "Standard", 10m);
        await SeedShippingMethodAsync("US", "TX", baseRate: 5m, ratePerKg: 1m);

        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"confirm.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Confirm", "Order");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "TX");

        var (reviewPageHtml, _) = await ReachReviewAsync(client, addressId);
        var (reviewAddressId, shippingMethodId, idempotencyKey) = ExtractReviewFormValues(reviewPageHtml);

        var placeOrderResponse = await PostPlaceOrderAsync(client, reviewPageHtml, reviewAddressId, shippingMethodId, idempotencyKey);
        var body = await placeOrderResponse.Content.ReadAsStringAsync();

        body.Should().Contain("Your order details have been validated");
        body.Should().Contain("117.00");
    }

    [Fact]
    public async Task Resubmitting_the_same_idempotency_key_replays_the_original_outcome_instead_of_re_validating()
    {
        var product = await SeedProductAsync(price: 100m);
        // Distinct region (WA) - see the comment in the insufficient-stock
        // test above for why each test needs its own jurisdiction here.
        await SeedShippingMethodAsync("US", "WA", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 5, allowBackorder: false);

        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"idempotent.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Idem", "Potent");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "WA");

        var (reviewPageHtml, _) = await ReachReviewAsync(client, addressId);
        var (reviewAddressId, shippingMethodId, idempotencyKey) = ExtractReviewFormValues(reviewPageHtml);

        var firstResponse = await PostPlaceOrderAsync(client, reviewPageHtml, reviewAddressId, shippingMethodId, idempotencyKey);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        firstBody.Should().Contain("Your order details have been validated");

        // Stock now depletes - a fresh validation attempt would fail, but
        // resubmitting the exact same idempotency key should still replay
        // the already-successful outcome rather than re-validating.
        await SetInventoryOnHandAsync(product.Id, onHand: 0);

        var secondResponse = await PostPlaceOrderAsync(client, reviewPageHtml, reviewAddressId, shippingMethodId, idempotencyKey);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        secondBody.Should().Contain("Your order details have been validated");
        secondBody.Should().NotContain("exceed available stock");
    }

    [Fact]
    public async Task Placing_the_order_with_a_stale_shipping_method_id_redirects_back_to_the_shipping_step()
    {
        var product = await SeedProductAsync(price: 100m);
        // Distinct region (NV) - see the comment in the insufficient-stock
        // test above for why each test needs its own jurisdiction here.
        await SeedShippingMethodAsync("US", "NV", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync($"staleship.{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Stale", "Ship");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "NV");

        var (reviewPageHtml, _) = await ReachReviewAsync(client, addressId);
        var (reviewAddressId, _, idempotencyKey) = ExtractReviewFormValues(reviewPageHtml);

        var placeOrderResponse = await PostPlaceOrderAsync(client, reviewPageHtml, reviewAddressId, shippingMethodId: 999999, idempotencyKey);
        var body = await placeOrderResponse.Content.ReadAsStringAsync();

        body.Should().Contain("Step 2 of 3");
        body.Should().Contain("Please choose a shipping method");
    }

    private static (int AddressId, int ShippingMethodId, string IdempotencyKey) ExtractReviewFormValues(string reviewHtml)
    {
        var addressId = int.Parse(System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"addressId\" value=\"(\\d+)\"").Groups[1].Value);
        var shippingMethodId = int.Parse(System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"shippingMethodId\" value=\"(\\d+)\"").Groups[1].Value);
        var idempotencyKey = System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"idempotencyKey\" value=\"([^\"]+)\"").Groups[1].Value;
        return (addressId, shippingMethodId, idempotencyKey);
    }

    private static Task<HttpResponseMessage> PostPlaceOrderAsync(
        HttpClient client, string reviewPageHtml, int addressId, int shippingMethodId, string idempotencyKey)
    {
        var token = HtmlHelpers.ExtractAntiForgeryToken(reviewPageHtml);
        return client.PostAsync("/Checkout/PlaceOrder", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["addressId"] = addressId.ToString(),
            ["shippingMethodId"] = shippingMethodId.ToString(),
            ["idempotencyKey"] = idempotencyKey,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private async Task<(string ReviewHtml, int AddressId)> ReachReviewAsync(HttpClient client, int addressId)
    {
        var indexPageHtml = await client.GetStringAsync("/Checkout");
        var indexToken = HtmlHelpers.ExtractAntiForgeryToken(indexPageHtml);
        var toShippingResponse = await client.PostAsync("/Checkout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["addressId"] = addressId.ToString(), ["__RequestVerificationToken"] = indexToken }));
        var shippingPageHtml = await toShippingResponse.Content.ReadAsStringAsync();
        var shippingMethodId = ExtractFirstShippingMethodId(shippingPageHtml);

        var shippingToken = HtmlHelpers.ExtractAntiForgeryToken(shippingPageHtml);
        var toReviewResponse = await client.PostAsync("/Checkout/Shipping", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["addressId"] = addressId.ToString(),
                ["shippingMethodId"] = shippingMethodId.ToString(),
                ["__RequestVerificationToken"] = shippingToken,
            }));
        var reviewPageHtml = await toReviewResponse.Content.ReadAsStringAsync();
        return (reviewPageHtml, addressId);
    }

    private static int ExtractFirstShippingMethodId(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html, "name=\"shippingMethodId\"[^>]*value=\"(\\d+)\"");
        return int.Parse(match.Groups[1].Value);
    }

    private static async Task<int> CreateAddressAsync(HttpClient client, string countryCode, string regionCode, string city = "Springfield")
    {
        var createPageResponse = await client.GetAsync("/Addresses/Create");
        var createPageHtml = await createPageResponse.Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(createPageHtml);

        var formValues = new Dictionary<string, string>
        {
            ["Label"] = "Home",
            ["FullName"] = "Jane Doe",
            ["Phone"] = "555-0100",
            ["Line1"] = "123 Main St",
            ["City"] = city,
            ["RegionCode"] = regionCode,
            ["PostalCode"] = "90210",
            ["CountryCode"] = countryCode,
            ["__RequestVerificationToken"] = token,
        };

        await client.PostAsync("/Addresses/Create", new FormUrlEncodedContent(formValues));

        var indexHtml = await client.GetStringAsync("/Addresses");
        var match = System.Text.RegularExpressions.Regex.Match(indexHtml, "/Addresses/Edit/(\\d+)");
        return int.Parse(match.Groups[1].Value);
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

    private async Task<Product> SeedProductAsync(decimal price = 50m, decimal? weight = null)
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

    private async Task SeedTaxRateAsync(string countryCode, string regionCode, string taxCategory, decimal ratePercent)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.TaxRates.Add(new Domain.Taxation.TaxRate
        {
            CountryCode = countryCode,
            RegionCode = regionCode,
            TaxCategory = taxCategory,
            RatePercent = ratePercent,
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedInventoryAsync(int productId, int onHand, bool allowBackorder)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var warehouse = new Domain.Inventory.Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        dbContext.InventoryItems.Add(new Domain.Inventory.InventoryItem
        {
            WarehouseId = warehouse.Id,
            ProductId = productId,
            QuantityOnHand = onHand,
            QuantityReserved = 0,
            AllowBackorder = allowBackorder,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task SetInventoryOnHandAsync(int productId, int onHand)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = await dbContext.InventoryItems.FirstAsync(i => i.ProductId == productId);
        item.QuantityOnHand = onHand;
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedShippingMethodAsync(string countryCode, string regionCode, decimal baseRate, decimal ratePerKg)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.ShippingMethods.Add(new Domain.Shipping.ShippingMethod
        {
            // Guid-suffixed so multiple tests seeding the same jurisdiction
            // within one shared-DB test run (reset once per collection, not
            // per test) don't collide on ShippingMethod's (CountryCode,
            // RegionCode, Name) uniqueness - Contain("Standard Shipping")
            // assertions still match since it's just a suffix.
            Name = $"Standard Shipping {Guid.NewGuid():N}",
            CountryCode = countryCode,
            RegionCode = regionCode,
            BaseRate = baseRate,
            RatePerKg = ratePerKg,
            DisplayOrder = 0,
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();
    }
}
