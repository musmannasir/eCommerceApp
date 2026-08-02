using System.Text.RegularExpressions;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the customer-facing "My Orders" dashboard (Milestone 11.1) over
/// real HTTP - proves the [Authorize] gate, ownership isolation (one
/// customer never sees another's orders or totals), and that TotalSpent
/// only counts a successfully-charged order.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class MyOrdersFlowTests
{
    private readonly AuthTestFixture _fixture;

    public MyOrdersFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_anonymous_visitor_loading_my_orders_is_redirected_to_login()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Orders");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in");
    }

    [Fact]
    public async Task A_signed_in_customer_with_no_orders_sees_an_empty_state()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"noorders.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "No", "Orders");

        var response = await client.GetAsync("/Orders");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("haven't placed any orders yet");
    }

    [Fact]
    public async Task A_customer_sees_only_their_own_order_and_total_spent_excludes_a_declined_charge()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "ND", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"myorders.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "My", "Orders");
        var addressId = await CreateAddressAsync(client, "US", "ND");

        // A declined order, then a successful one - TotalSpent should only
        // reflect the successful charge, but TotalOrders counts both. A
        // decline never clears the cart (Milestone 9.2), so the same single
        // line is reused for the second, successful submission rather than
        // adding to the cart again.
        await AddToCartAsync(client, product.Id);
        await PlaceOrderAsync(client, addressId, cardNumber: "4000000000000002");
        await PlaceOrderAsync(client, addressId, cardNumber: "4242424242424242");

        var myOrdersHtml = await client.GetStringAsync("/Orders");
        myOrdersHtml.Should().Contain("Paid").And.Contain("PaymentFailed");

        var totalOrdersMatch = Regex.Match(myOrdersHtml, "Total orders[\\s\\S]*?fs-4\">(\\d+)");
        var totalSpentMatch = Regex.Match(myOrdersHtml, "Total spent[\\s\\S]*?fs-4\">([\\d.]+)");
        int.Parse(totalOrdersMatch.Groups[1].Value).Should().Be(2);
        decimal.Parse(totalSpentMatch.Groups[1].Value).Should().Be(105.00m);

        // A different customer's dashboard shows none of this.
        var otherClient = _fixture.Factory.CreateClient();
        var otherEmail = $"otherbuyer.{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterViaFormAsync(otherEmail, "Str0ng!Passw0rd", "Other", "Buyer");
        var otherHtml = await otherClient.GetStringAsync("/Orders");
        otherHtml.Should().Contain("haven't placed any orders yet");
    }

    [Fact]
    public async Task A_customer_can_view_their_own_order_detail_page_with_tracking()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "SD", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"orderdetail.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Order", "Detail");
        var addressId = await CreateAddressAsync(client, "US", "SD");

        await AddToCartAsync(client, product.Id);
        var orderNumber = await PlaceOrderAsync(client, addressId, cardNumber: "4242424242424242");

        var detailsHtml = await client.GetStringAsync($"/Orders/{orderNumber}");

        detailsHtml.Should().Contain(orderNumber).And.Contain("Placed").And.Contain("Paid").And.Contain("Print invoice");
    }

    [Fact]
    public async Task A_customer_cannot_view_another_customers_order_by_guessing_the_order_number()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "VT", baseRate: 5m, ratePerKg: 0m);

        var ownerClient = _fixture.Factory.CreateClient();
        var ownerEmail = $"orderowner.{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterViaFormAsync(ownerEmail, "Str0ng!Passw0rd", "Order", "Owner");
        var addressId = await CreateAddressAsync(ownerClient, "US", "VT");
        await AddToCartAsync(ownerClient, product.Id);
        var orderNumber = await PlaceOrderAsync(ownerClient, addressId, cardNumber: "4242424242424242");

        var otherClient = _fixture.Factory.CreateClient();
        var otherEmail = $"orderintruder.{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterViaFormAsync(otherEmail, "Str0ng!Passw0rd", "Order", "Intruder");

        var detailsResponse = await otherClient.GetAsync($"/Orders/{orderNumber}");
        var invoiceResponse = await otherClient.GetAsync($"/Orders/{orderNumber}/Invoice");

        ((int)detailsResponse.StatusCode).Should().Be(404);
        ((int)invoiceResponse.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task A_customer_can_print_the_invoice_for_a_paid_order()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "WY", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"invoice.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Invoice", "Test");
        var addressId = await CreateAddressAsync(client, "US", "WY");

        await AddToCartAsync(client, product.Id);
        var orderNumber = await PlaceOrderAsync(client, addressId, cardNumber: "4242424242424242");

        var invoiceHtml = await client.GetStringAsync($"/Orders/{orderNumber}/Invoice");

        invoiceHtml.Should().Contain("Invoice").And.Contain(orderNumber).And.Contain(product.Name);
    }

    [Fact]
    public async Task Invoice_is_not_available_for_a_declined_order()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "ME", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"noinvoice.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "No", "Invoice");
        var addressId = await CreateAddressAsync(client, "US", "ME");

        await AddToCartAsync(client, product.Id);
        var orderNumber = await PlaceOrderAsync(client, addressId, cardNumber: "4000000000000002");

        var invoiceResponse = await client.GetAsync($"/Orders/{orderNumber}/Invoice");
        var body = await invoiceResponse.Content.ReadAsStringAsync();

        body.Should().Contain("only available for an order that was successfully charged");
    }

    [Fact]
    public async Task A_customer_can_reorder_a_past_orders_items_into_their_cart()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "NM", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"reorder.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Reorder", "Test");
        var addressId = await CreateAddressAsync(client, "US", "NM");

        await AddToCartAsync(client, product.Id);
        var orderNumber = await PlaceOrderAsync(client, addressId, cardNumber: "4242424242424242");

        var reorderResponse = await ReorderAsync(client, orderNumber);
        var cartHtml = await reorderResponse.Content.ReadAsStringAsync();

        reorderResponse.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/Cart");
        cartHtml.Should().Contain("Added 1 item").And.Contain(product.Name);
    }

    [Fact]
    public async Task Reordering_skips_a_product_that_was_deactivated_since_the_order_was_placed()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "NV", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"reorderskip.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Reorder", "Skip");
        var addressId = await CreateAddressAsync(client, "US", "NV");

        await AddToCartAsync(client, product.Id);
        var orderNumber = await PlaceOrderAsync(client, addressId, cardNumber: "4242424242424242");

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var toDeactivate = await dbContext.Products.FirstAsync(p => p.Id == product.Id);
            toDeactivate.IsActive = false;
            await dbContext.SaveChangesAsync();
        }

        var reorderResponse = await ReorderAsync(client, orderNumber);
        var cartHtml = await reorderResponse.Content.ReadAsStringAsync();

        cartHtml.Should().Contain("could be added to your cart").And.Contain(product.Name);
    }

    [Fact]
    public async Task A_customer_cannot_reorder_another_customers_order()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "NH", baseRate: 5m, ratePerKg: 0m);

        var ownerClient = _fixture.Factory.CreateClient();
        var ownerEmail = $"reorderowner.{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterViaFormAsync(ownerEmail, "Str0ng!Passw0rd", "Reorder", "Owner");
        var addressId = await CreateAddressAsync(ownerClient, "US", "NH");
        await AddToCartAsync(ownerClient, product.Id);
        var orderNumber = await PlaceOrderAsync(ownerClient, addressId, cardNumber: "4242424242424242");

        var otherClient = _fixture.Factory.CreateClient();
        var otherEmail = $"reorderintruder.{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterViaFormAsync(otherEmail, "Str0ng!Passw0rd", "Reorder", "Intruder");

        var detailsHtml = await otherClient.GetStringAsync("/"); // establish antiforgery cookie for the other client
        var homeToken = HtmlHelpers.ExtractMetaCsrfToken(detailsHtml);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/Orders/{orderNumber}/Reorder")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = homeToken }),
        };
        var response = await otherClient.SendAsync(request);

        ((int)response.StatusCode).Should().Be(404);
    }

    private static async Task<HttpResponseMessage> ReorderAsync(HttpClient client, string orderNumber)
    {
        var detailsHtml = await client.GetStringAsync($"/Orders/{orderNumber}");
        var token = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);

        return await client.PostAsync($"/Orders/{orderNumber}/Reorder", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
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
            ["FullName"] = "My Orders",
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
            ["cardholderName"] = "My Orders",
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

    private async Task SeedShippingMethodAsync(string countryCode, string regionCode, decimal baseRate, decimal ratePerKg)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.ShippingMethods.Add(new Domain.Shipping.ShippingMethod
        {
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
