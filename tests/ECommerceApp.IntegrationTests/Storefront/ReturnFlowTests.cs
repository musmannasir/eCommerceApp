using System.Text.RegularExpressions;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the return-request flow (Milestone 13.2) over real HTTP - customer
/// submission on a Delivered order, the admin queue, and Approve/Reject.
/// Admin-area authorization mirrors ReviewModerationFlowTests' shape exactly,
/// since the queue reuses Policies.CanManageOrders.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ReturnFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ReturnFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_return_queue()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Returns/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Return Requests");
    }

    [Fact]
    public async Task Customer_cannot_view_the_return_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.returns.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Returns/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CustomerSupport_can_view_the_return_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customersupport.returns.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CustomerSupport");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Returns/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Return Requests");
    }

    [Fact]
    public async Task A_customer_can_request_a_return_on_a_delivered_order_and_have_it_approved()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "CO", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"returner.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Return", "Customer");
        var orderNumber = await PlaceAnOrderAsync(customerClient, product.Id, "US", "CO");

        await DeliverOrderAsync(orderNumber);

        var returnFormHtml = await customerClient.GetStringAsync($"/Orders/{orderNumber}/Return");
        var itemIdMatch = Regex.Match(returnFormHtml, "Items\\[0\\]\\.OrderItemId\" value=\"(\\d+)\"");
        var orderItemId = int.Parse(itemIdMatch.Groups[1].Value);
        var token = HtmlHelpers.ExtractAntiForgeryToken(returnFormHtml);

        var submitResponse = await customerClient.PostAsync($"/Orders/{orderNumber}/Return", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Reason"] = "Defective",
                ["Comment"] = "Arrived broken",
                ["Items[0].OrderItemId"] = orderItemId.ToString(),
                ["Items[0].Quantity"] = "1",
                ["__RequestVerificationToken"] = token,
            }));
        var afterSubmitHtml = await submitResponse.Content.ReadAsStringAsync();

        afterSubmitHtml.Should().Contain("Your return request has been submitted.").And.Contain("Requested");
        afterSubmitHtml.Should().NotContain("Request a return");

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.returns.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "CustomerSupport");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var queueHtml = await adminClient.GetStringAsync("/Admin/Returns/Index");
        queueHtml.Should().Contain(orderNumber).And.Contain("Arrived broken");

        var approveToken = HtmlHelpers.ExtractAntiForgeryToken(queueHtml);
        var returnRequestId = int.Parse(Regex.Match(queueHtml, "/Admin/Returns/Approve/(\\d+)").Groups[1].Value);
        var approveResponse = await adminClient.PostAsync("/Admin/Returns/Approve", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = returnRequestId.ToString(), ["__RequestVerificationToken"] = approveToken }));
        var afterApproveHtml = await approveResponse.Content.ReadAsStringAsync();

        afterApproveHtml.Should().Contain("Return request approved.").And.NotContain(orderNumber);

        var customerDetailsHtml = await customerClient.GetStringAsync($"/Orders/{orderNumber}");
        customerDetailsHtml.Should().Contain("Approved");
    }

    [Fact]
    public async Task Rejecting_a_return_request_shows_the_reason_and_allows_resubmission()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "UT", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"rejected.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Rejected", "Customer");
        var orderNumber = await PlaceAnOrderAsync(customerClient, product.Id, "US", "UT");

        await DeliverOrderAsync(orderNumber);
        await SubmitReturnRequestAsync(customerClient, orderNumber, "Arrived broken");

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.reject.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "CustomerSupport");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var queueHtml = await adminClient.GetStringAsync("/Admin/Returns/Index");
        var rejectToken = HtmlHelpers.ExtractAntiForgeryToken(queueHtml);
        var returnRequestId = int.Parse(Regex.Match(queueHtml, "/Admin/Returns/Reject/(\\d+)").Groups[1].Value);
        await adminClient.PostAsync("/Admin/Returns/Reject", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = returnRequestId.ToString(),
                ["rejectionReason"] = "Item is outside the return policy.",
                ["__RequestVerificationToken"] = rejectToken,
            }));

        var customerDetailsHtml = await customerClient.GetStringAsync($"/Orders/{orderNumber}");
        customerDetailsHtml.Should().Contain("Rejected").And.Contain("Item is outside the return policy.")
            .And.Contain("Request a return");

        var secondSubmitHtml = await SubmitReturnRequestAsync(customerClient, orderNumber, "No longer needed", "NoLongerNeeded");
        secondSubmitHtml.Should().Contain("Your return request has been submitted.");
    }

    [Fact]
    public async Task A_customer_cannot_request_a_return_for_another_customers_order()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "NY", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var ownerClient = _fixture.Factory.CreateClient();
        var ownerEmail = $"returnowner.{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterViaFormAsync(ownerEmail, "Str0ng!Passw0rd", "Return", "Owner");
        var orderNumber = await PlaceAnOrderAsync(ownerClient, product.Id, "US", "NY");
        await DeliverOrderAsync(orderNumber);

        var intruderClient = _fixture.Factory.CreateClient();
        var intruderEmail = $"returnintruder.{Guid.NewGuid():N}@example.com";
        await intruderClient.RegisterViaFormAsync(intruderEmail, "Str0ng!Passw0rd", "Return", "Intruder");

        var formResponse = await intruderClient.GetAsync($"/Orders/{orderNumber}/Return");

        ((int)formResponse.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Requesting_a_return_for_a_non_delivered_order_shows_a_validation_error()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "OH", baseRate: 5m, ratePerKg: 0m);

        var client = _fixture.Factory.CreateClient();
        var email = $"notdelivered.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Not", "Delivered");
        var orderNumber = await PlaceAnOrderAsync(client, product.Id, "US", "OH");

        var response = await client.GetAsync($"/Orders/{orderNumber}/Return");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Only a delivered order can be returned.");
    }

    private static async Task<string> SubmitReturnRequestAsync(HttpClient client, string orderNumber, string comment, string reason = "Defective")
    {
        var formHtml = await client.GetStringAsync($"/Orders/{orderNumber}/Return");
        var orderItemId = int.Parse(Regex.Match(formHtml, "Items\\[0\\]\\.OrderItemId\" value=\"(\\d+)\"").Groups[1].Value);
        var token = HtmlHelpers.ExtractAntiForgeryToken(formHtml);

        var response = await client.PostAsync($"/Orders/{orderNumber}/Return", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Reason"] = reason,
                ["Comment"] = comment,
                ["Items[0].OrderItemId"] = orderItemId.ToString(),
                ["Items[0].Quantity"] = "1",
                ["__RequestVerificationToken"] = token,
            }));
        return await response.Content.ReadAsStringAsync();
    }

    private async Task DeliverOrderAsync(string orderNumber)
    {
        var staffClient = _fixture.Factory.CreateClient();
        var staffEmail = $"ordermanager.deliver.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(staffEmail, "Str0ng!Passw0rd", "OrderManager");
        await staffClient.LoginViaFormAsync(staffEmail, "Str0ng!Passw0rd");

        var indexHtml = await staffClient.GetStringAsync($"/Admin/Orders?search={orderNumber}");
        var orderId = int.Parse(Regex.Match(indexHtml, "/Admin/Orders/Details/(\\d+)").Groups[1].Value);

        var detailsHtml = await staffClient.GetStringAsync($"/Admin/Orders/Details/{orderId}");
        var shipToken = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);
        var shipResponse = await staffClient.PostAsync("/Admin/Orders/Ship", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = orderId.ToString(),
                ["carrier"] = "UPS",
                ["trackingNumber"] = "1Z999AA10123456784",
                ["__RequestVerificationToken"] = shipToken,
            }));
        var afterShipHtml = await shipResponse.Content.ReadAsStringAsync();

        var deliverToken = HtmlHelpers.ExtractAntiForgeryToken(afterShipHtml);
        await staffClient.PostAsync("/Admin/Orders/MarkDelivered", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = orderId.ToString(), ["__RequestVerificationToken"] = deliverToken }));
    }

    private async Task<string> PlaceAnOrderAsync(HttpClient client, int productId, string countryCode, string regionCode, string cardNumber = "4242424242424242")
    {
        await AddToCartAsync(client, productId);
        var addressId = await CreateAddressAsync(client, countryCode, regionCode);

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
            ["cardholderName"] = "Jane Doe",
            ["expiryMonth"] = "12",
            ["expiryYear"] = "2030",
            ["cvv"] = "123",
            ["__RequestVerificationToken"] = placeToken,
        }));

        return placeOrderResponse.RequestMessage!.RequestUri!.AbsolutePath.Split('/').Last();
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
            ["FullName"] = "Jane Doe",
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

    private async Task SeedInventoryAsync(int productId, int onHand)
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
            AllowBackorder = false,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }
}
