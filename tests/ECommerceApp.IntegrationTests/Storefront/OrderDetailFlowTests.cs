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
/// Drives the admin order detail page (Milestone 10.2) end-to-end - viewing
/// a real placed order, saving internal notes, and cancelling a paid order
/// (which releases its stock reservation and does not process a refund).
/// </summary>
[Collection(AuthTestCollection.Name)]
public class OrderDetailFlowTests
{
    private readonly AuthTestFixture _fixture;

    public OrderDetailFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Staff_can_view_order_details_save_notes_and_cancel_a_paid_order()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "WY", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var orderNumber = await PlaceAnOrderAsync(product.Id, "US", "WY");

        var staffClient = _fixture.Factory.CreateClient();
        var staffEmail = $"ordermanager.detail.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(staffEmail, "Str0ng!Passw0rd", "OrderManager");
        await staffClient.LoginViaFormAsync(staffEmail, "Str0ng!Passw0rd");

        var indexHtml = await staffClient.GetStringAsync($"/Admin/Orders?search={orderNumber}");
        var orderId = ExtractOrderIdFromDetailsLink(indexHtml);

        var detailsHtml = await staffClient.GetStringAsync($"/Admin/Orders/Details/{orderId}");
        detailsHtml.Should().Contain(orderNumber).And.Contain("Cancel order");

        var notesToken = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);
        var notesResponse = await staffClient.PostAsync("/Admin/Orders/UpdateNotes", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = orderId.ToString(),
                ["notes"] = "Called customer to confirm address.",
                ["__RequestVerificationToken"] = notesToken,
            }));
        var afterNotesHtml = await notesResponse.Content.ReadAsStringAsync();
        afterNotesHtml.Should().Contain("Called customer to confirm address.");

        var cancelToken = HtmlHelpers.ExtractAntiForgeryToken(afterNotesHtml);
        var cancelResponse = await staffClient.PostAsync("/Admin/Orders/Cancel", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = orderId.ToString(), ["__RequestVerificationToken"] = cancelToken }));
        var afterCancelHtml = await cancelResponse.Content.ReadAsStringAsync();

        afterCancelHtml.Should().Contain("Order cancelled").And.NotContain("Cancel order");

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await dbContext.Orders.SingleAsync(o => o.OrderNumber == orderNumber);
        order.Status.Should().Be(OrderStatus.Cancelled);

        var item = await dbContext.InventoryItems.SingleAsync(i => i.ProductId == product.Id);
        item.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Staff_can_ship_a_paid_order_and_then_mark_it_delivered()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "MT", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var orderNumber = await PlaceAnOrderAsync(product.Id, "US", "MT");

        var staffClient = _fixture.Factory.CreateClient();
        var staffEmail = $"ordermanager.ship.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(staffEmail, "Str0ng!Passw0rd", "OrderManager");
        await staffClient.LoginViaFormAsync(staffEmail, "Str0ng!Passw0rd");

        var indexHtml = await staffClient.GetStringAsync($"/Admin/Orders?search={orderNumber}");
        var orderId = ExtractOrderIdFromDetailsLink(indexHtml);

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

        afterShipHtml.Should().Contain("Order marked as shipped.").And.Contain("UPS").And.Contain("1Z999AA10123456784")
            .And.NotContain("Cancel order").And.Contain("Mark delivered");

        var deliverToken = HtmlHelpers.ExtractAntiForgeryToken(afterShipHtml);
        var deliverResponse = await staffClient.PostAsync("/Admin/Orders/MarkDelivered", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = orderId.ToString(), ["__RequestVerificationToken"] = deliverToken }));
        var afterDeliverHtml = await deliverResponse.Content.ReadAsStringAsync();

        afterDeliverHtml.Should().Contain("Order marked as delivered.").And.Contain("Delivered");

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await dbContext.Orders.SingleAsync(o => o.OrderNumber == orderNumber);
        order.Status.Should().Be(OrderStatus.Delivered);

        var item = await dbContext.InventoryItems.SingleAsync(i => i.ProductId == product.Id);
        item.QuantityOnHand.Should().Be(9);
        item.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Cancel_is_rejected_for_an_order_that_is_not_paid()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "VT", baseRate: 5m, ratePerKg: 0m);

        var orderNumber = await PlaceAnOrderAsync(product.Id, "US", "VT", cardNumber: "4000000000000002");

        var staffClient = _fixture.Factory.CreateClient();
        var staffEmail = $"ordermanager.nocancel.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(staffEmail, "Str0ng!Passw0rd", "OrderManager");
        await staffClient.LoginViaFormAsync(staffEmail, "Str0ng!Passw0rd");

        var indexHtml = await staffClient.GetStringAsync($"/Admin/Orders?search={orderNumber}");
        var orderId = ExtractOrderIdFromDetailsLink(indexHtml);

        var detailsHtml = await staffClient.GetStringAsync($"/Admin/Orders/Details/{orderId}");
        detailsHtml.Should().NotContain("Cancel order");

        var token = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);
        var cancelResponse = await staffClient.PostAsync("/Admin/Orders/Cancel", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = orderId.ToString(), ["__RequestVerificationToken"] = token }));
        var body = await cancelResponse.Content.ReadAsStringAsync();

        body.Should().Contain("Only a paid order can be cancelled.");
    }

    private static int ExtractOrderIdFromDetailsLink(string html)
    {
        var match = Regex.Match(html, "/Admin/Orders/Details/(\\d+)");
        return int.Parse(match.Groups[1].Value);
    }

    private async Task<string> PlaceAnOrderAsync(int productId, string countryCode, string regionCode, string cardNumber = "4242424242424242")
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"orderdetail.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Order", "Detail");
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
