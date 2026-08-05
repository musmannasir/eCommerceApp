using System.Text.RegularExpressions;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the admin reports hub, the Top Selling Products report, and CSV
/// export on all three finance/reporting pages (Milestone 14.3) over real
/// HTTP. Reuses the CanViewFinancialReports gate Ledger/Cash Flow already
/// established (Milestones 14.1/14.2).
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ReportsFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ReportsFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_reports()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Reports/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("<h1 class=\"h3 mb-0\">Reports</h1>");
    }

    [Fact]
    public async Task OrderManager_cannot_view_reports()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.reports.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Reports/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Admin_can_view_the_reports_hub_and_navigate_to_top_selling_products()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.reportshub.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var indexResponse = await client.GetAsync("/Admin/Reports/Index");
        var indexBody = await indexResponse.Content.ReadAsStringAsync();
        indexResponse.IsSuccessStatusCode.Should().BeTrue();
        indexBody.Should().Contain("Reports").And.Contain("Top Selling Products").And.Contain("Ledger").And.Contain("Cash Flow");

        var reportResponse = await client.GetAsync("/Admin/Reports/TopSellingProducts");
        var reportBody = await reportResponse.Content.ReadAsStringAsync();
        reportResponse.IsSuccessStatusCode.Should().BeTrue();
        reportBody.Should().Contain("Top Selling Products");
    }

    [Fact]
    public async Task A_real_order_shows_up_on_the_top_selling_products_report_and_its_csv_export()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "OK", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"reportscustomer.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Reports", "Customer");
        await PlaceAnOrderAsync(customerClient, product.Id, "US", "OK");

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.topselling.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "Admin");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var reportHtml = await adminClient.GetStringAsync("/Admin/Reports/TopSellingProducts");
        reportHtml.Should().Contain(product.Name);

        var csvResponse = await adminClient.GetAsync("/Admin/Reports/TopSellingProductsExportCsv");
        csvResponse.IsSuccessStatusCode.Should().BeTrue();
        csvResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csvBody = await csvResponse.Content.ReadAsStringAsync();
        csvBody.Should().Contain("ProductId,ProductName,QuantitySold,Revenue").And.Contain(product.Name);
    }

    [Fact]
    public async Task Ledger_csv_export_returns_a_csv_file_with_a_real_transaction()
    {
        var product = await SeedProductAsync();
        await SeedShippingMethodAsync("US", "OR", baseRate: 5m, ratePerKg: 0m);
        await SeedInventoryAsync(product.Id, onHand: 10);

        var customerClient = _fixture.Factory.CreateClient();
        var email = $"ledgercsvcustomer.{Guid.NewGuid():N}@example.com";
        await customerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "LedgerCsv", "Customer");
        var orderNumber = await PlaceAnOrderAsync(customerClient, product.Id, "US", "OR");

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.ledgercsv.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "Admin");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var response = await adminClient.GetAsync("/Admin/Ledger/ExportCsv");
        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Date,Type,Order,Amount").And.Contain(orderNumber).And.Contain("Charge");
    }

    [Fact]
    public async Task Cash_flow_csv_export_returns_a_csv_file()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.cashflowcsv.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Ledger/CashFlowExportCsv");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Date,Revenue,Refunded,Net");
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
