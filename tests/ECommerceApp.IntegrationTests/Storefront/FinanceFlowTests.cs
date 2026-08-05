using System.Text.RegularExpressions;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the admin financial dashboard and ledger (Milestone 14.1) over
/// real HTTP - authorization for the new CanViewFinancialReports policy
/// (pre-wired since Milestone 1, first actually used here), the dashboard's
/// role-conditional financial cards, the merged ledger feed, and the
/// tightened CanProcessRefunds gate on the Refund action.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class FinanceFlowTests
{
    private readonly AuthTestFixture _fixture;

    public FinanceFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_ledger()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Ledger/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("<h1 class=\"h3 mb-0\">Ledger</h1>");
    }

    [Fact]
    public async Task OrderManager_cannot_view_the_ledger()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.ledger.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Ledger/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Admin_can_view_the_ledger()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.ledger.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Ledger/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Ledger");
    }

    [Fact]
    public async Task OrderManager_cannot_view_cash_flow()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.cashflow.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Ledger/CashFlow");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Admin_can_view_cash_flow_and_a_real_order_shows_up_on_todays_row()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "CT", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"cashflowcustomer.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "CashFlow", "Customer");
        await PlaceAnOrderAsync(customerClient, product.Id, "US", "CT");

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.cashflow.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "Admin");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var response = await adminClient.GetAsync("/Admin/Ledger/CashFlow");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        var today = DateTime.UtcNow.Date;

        // Cash flow totals the whole site's revenue for the day, not just
        // this test's own order - other tests in this shared, only-reset-
        // once-per-collection database also place real orders "today", so
        // the expected figure is computed independently via SQL rather than
        // hardcoded, the same way ReturnFlowTests confirms refund amounts.
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expectedRevenue = await dbContext.Payments
            .Where(p => p.Status == Domain.Payments.PaymentStatus.Succeeded && p.ProcessedAtUtc >= today && p.ProcessedAtUtc < today.AddDays(1))
            .SumAsync(p => p.Amount);

        body.Should().Contain("Cash Flow").And.Contain(today.ToString("yyyy-MM-dd")).And.Contain(expectedRevenue.ToString("0.00"));
    }

    [Fact]
    public async Task Cash_flow_respects_an_explicit_date_range()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.cashflowrange.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var from = DateTime.UtcNow.Date.AddDays(-3).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var response = await client.GetAsync($"/Admin/Ledger/CashFlow?from={from}&to={to}");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain(from).And.Contain(to);
    }

    [Fact]
    public async Task The_dashboard_hides_financial_cards_from_a_role_without_CanViewFinancialReports()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.dashboard.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().NotContain("Total revenue");
    }

    [Fact]
    public async Task The_dashboard_shows_financial_cards_to_an_Admin()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.dashboard.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Total revenue").And.Contain("Total refunded").And.Contain("Net revenue");
    }

    [Fact]
    public async Task A_real_order_and_refund_show_up_on_the_dashboard_and_ledger()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "LA", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"financecustomer.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Finance", "Customer");
        var orderNumber = await PlaceAnOrderAsync(customerClient, product.Id, "US", "LA");

        await DeliverOrderAsync(orderNumber);
        await SubmitReturnRequestAsync(customerClient, orderNumber, "Changed my mind");

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.financeflow.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "Admin");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var queueHtml = await adminClient.GetStringAsync("/Admin/Returns/Index");
        var approveToken = HtmlHelpers.ExtractAntiForgeryToken(queueHtml);
        var returnRequestId = int.Parse(Regex.Match(queueHtml, $@"{Regex.Escape(orderNumber)}[\s\S]*?/Admin/Returns/Approve/(\d+)").Groups[1].Value);
        await adminClient.PostAsync("/Admin/Returns/Approve", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = returnRequestId.ToString(), ["__RequestVerificationToken"] = approveToken }));

        var awaitingHtml = await adminClient.GetStringAsync("/Admin/Returns/Index");
        var refundToken = HtmlHelpers.ExtractAntiForgeryToken(awaitingHtml);
        await adminClient.PostAsync("/Admin/Returns/Refund", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = returnRequestId.ToString(), ["__RequestVerificationToken"] = refundToken }));

        var ledgerHtml = await adminClient.GetStringAsync("/Admin/Ledger/Index");
        ledgerHtml.Should().Contain(orderNumber).And.Contain("Charge").And.Contain("Refund");

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refund = await dbContext.Refunds.SingleAsync(r => r.Order.OrderNumber == orderNumber);
        refund.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task CustomerSupport_can_approve_a_return_but_cannot_refund_it()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "MI", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"gatecustomer.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Gate", "Customer");
        var orderNumber = await PlaceAnOrderAsync(customerClient, product.Id, "US", "MI");

        await DeliverOrderAsync(orderNumber);
        await SubmitReturnRequestAsync(customerClient, orderNumber, "Wrong size");

        var supportClient = _fixture.Factory.CreateClient();
        var supportEmail = $"support.gate.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(supportEmail, "Str0ng!Passw0rd", "CustomerSupport");
        await supportClient.LoginViaFormAsync(supportEmail, "Str0ng!Passw0rd");

        var queueHtml = await supportClient.GetStringAsync("/Admin/Returns/Index");
        var approveToken = HtmlHelpers.ExtractAntiForgeryToken(queueHtml);
        var returnRequestId = int.Parse(Regex.Match(queueHtml, $@"{Regex.Escape(orderNumber)}[\s\S]*?/Admin/Returns/Approve/(\d+)").Groups[1].Value);
        var approveResponse = await supportClient.PostAsync("/Admin/Returns/Approve", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = returnRequestId.ToString(), ["__RequestVerificationToken"] = approveToken }));

        approveResponse.IsSuccessStatusCode.Should().BeTrue();

        var awaitingHtml = await supportClient.GetStringAsync("/Admin/Returns/Index");
        var refundToken = HtmlHelpers.ExtractAntiForgeryToken(awaitingHtml);
        var refundResponse = await supportClient.PostAsync("/Admin/Returns/Refund", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = returnRequestId.ToString(), ["__RequestVerificationToken"] = refundToken }));

        ((int)refundResponse.StatusCode).Should().Be(403);
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
        var staffEmail = $"ordermanager.financedeliver.{Guid.NewGuid():N}@example.com";
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
