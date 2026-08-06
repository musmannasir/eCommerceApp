using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Notifications;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Milestone 15.1 - drives the two real email touchpoints (forgot-password,
/// order confirmation) over real HTTP and checks the actual rendered output
/// that DevEmailSender writes to disk, rather than mocking IEmailSender -
/// this exercises the real Razor template rendering pipeline end to end, the
/// same in-process host both the app and the test run under (WebApplicationFactory),
/// so Directory.GetCurrentDirectory() resolves to the same "Logs/DevEmails" path here.
/// These assertions are unchanged by Milestone 15.2's transactional-outbox
/// rework - proof the outbox is purely an internal durability layer, not a
/// user-visible behavior change - with new OutboxMessage-row assertions
/// added alongside them.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class NotificationFlowTests
{
    private readonly AuthTestFixture _fixture;
    private readonly string _devEmailsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "DevEmails");

    public NotificationFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Requesting_a_password_reset_writes_a_rendered_email_with_a_working_reset_link()
    {
        var email = $"resetpw.{Guid.NewGuid():N}@example.com";
        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Reset", "Pw");

        var forgotPageResponse = await client.GetAsync("/Account/ForgotPassword");
        var forgotPageHtml = await forgotPageResponse.Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(forgotPageHtml);

        var response = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["Email"] = email, ["__RequestVerificationToken"] = token }));
        response.IsSuccessStatusCode.Should().BeTrue();

        var emailContent = ReadMostRecentEmailFor(email);
        emailContent.Should().Contain("Reset your password");
        emailContent.Should().Contain($"/Account/ResetPassword?email={email}&amp;token=");

        // Milestone 15.2 - the outbox row it was durably enqueued as reached
        // Processed, confirming the transactional-outbox path actually ran
        // rather than the email having appeared some other way.
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await dbContext.OutboxMessages
            .Where(m => m.Type == OutboxMessageType.PasswordResetEmail)
            .OrderByDescending(m => m.Id)
            .FirstAsync();
        message.Status.Should().Be(OutboxMessageStatus.Processed);
        message.ProcessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Placing_a_paid_order_writes_a_rendered_order_confirmation_email()
    {
        var product = await SeedProductAsync(price: 100m, weight: 2m);
        await SeedShippingMethodAsync("US", "WI", baseRate: 5m, ratePerKg: 0m);

        var email = $"orderemail.{Guid.NewGuid():N}@example.com";
        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Order", "Email");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "WI");

        var (reviewPageHtml, _) = await ReachReviewAsync(client, addressId);
        var (reviewAddressId, shippingMethodId, idempotencyKey) = ExtractReviewFormValues(reviewPageHtml);

        var placeOrderResponse = await PostPlaceOrderAsync(client, reviewPageHtml, reviewAddressId, shippingMethodId, idempotencyKey);
        placeOrderResponse.EnsureSuccessStatusCode();
        var orderNumber = System.Text.RegularExpressions.Regex.Match(
            placeOrderResponse.RequestMessage!.RequestUri!.AbsolutePath, @"ORD-\d+").Value;

        var emailContent = ReadMostRecentEmailFor(email);
        emailContent.Should().Contain($"Order confirmation - {orderNumber}");
        emailContent.Should().Contain(orderNumber);
        emailContent.Should().Contain("105.00"); // 100 subtotal + 5 shipping, no tax rate seeded.

        // Milestone 15.2 - the outbox row OrderService.CreateOrderAsync
        // enqueued atomically with the Order/Payment rows reached Processed.
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await dbContext.OutboxMessages
            .Where(m => m.Type == OutboxMessageType.OrderConfirmationEmail)
            .OrderByDescending(m => m.Id)
            .FirstAsync();
        message.Status.Should().Be(OutboxMessageStatus.Processed);
        message.PayloadJson.Should().Contain(orderNumber);
    }

    [Fact]
    public async Task A_declined_payment_does_not_send_an_order_confirmation_email()
    {
        var product = await SeedProductAsync(price: 100m);
        await SeedShippingMethodAsync("US", "MN", baseRate: 5m, ratePerKg: 0m);

        var email = $"declinedemail.{Guid.NewGuid():N}@example.com";
        var client = _fixture.Factory.CreateClient();
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Declined", "Email");
        await AddToCartAsync(client, product.Id);
        var addressId = await CreateAddressAsync(client, "US", "MN");

        var (reviewPageHtml, _) = await ReachReviewAsync(client, addressId);
        var (reviewAddressId, shippingMethodId, idempotencyKey) = ExtractReviewFormValues(reviewPageHtml);

        var placeOrderResponse = await PostPlaceOrderAsync(
            client, reviewPageHtml, reviewAddressId, shippingMethodId, idempotencyKey, cardNumber: "4000000000000002");
        placeOrderResponse.EnsureSuccessStatusCode();

        // This test's email address is freshly generated (Guid-suffixed), so no
        // file for it can exist unless PlaceOrder just wrongly sent a confirmation.
        var safeRecipient = string.Concat(email.Split(Path.GetInvalidFileNameChars()));
        var matchingFiles = Directory.Exists(_devEmailsDirectory)
            ? Directory.GetFiles(_devEmailsDirectory, $"*_{safeRecipient}.html")
            : [];
        matchingFiles.Should().BeEmpty();

        // Milestone 15.2 - not just "no email file appeared" but confirming
        // OrderService never even enqueued an outbox row for this order in
        // the first place, at the source. The recipient email is
        // Guid-suffixed and unique to this test, so any match at all would
        // mean a confirmation was wrongly enqueued for this declined order.
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var anyMessageMentionsThisAddress = await dbContext.OutboxMessages
            .Where(m => m.Type == OutboxMessageType.OrderConfirmationEmail)
            .AnyAsync(m => m.PayloadJson.Contains(email));
        anyMessageMentionsThisAddress.Should().BeFalse();
    }

    private string ReadMostRecentEmailFor(string email)
    {
        var safeRecipient = string.Concat(email.Split(Path.GetInvalidFileNameChars()));
        var files = Directory.GetFiles(_devEmailsDirectory, $"*_{safeRecipient}.html");
        files.Should().NotBeEmpty($"DevEmailSender should have written a preview file for {email}");
        var mostRecent = files.OrderByDescending(f => f).First();
        return File.ReadAllText(mostRecent);
    }

    private static (int AddressId, int ShippingMethodId, string IdempotencyKey) ExtractReviewFormValues(string reviewHtml)
    {
        var addressId = int.Parse(System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"addressId\" value=\"(\\d+)\"").Groups[1].Value);
        var shippingMethodId = int.Parse(System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"shippingMethodId\" value=\"(\\d+)\"").Groups[1].Value);
        var idempotencyKey = System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"idempotencyKey\" value=\"([^\"]+)\"").Groups[1].Value;
        return (addressId, shippingMethodId, idempotencyKey);
    }

    private static Task<HttpResponseMessage> PostPlaceOrderAsync(
        HttpClient client, string reviewPageHtml, int addressId, int shippingMethodId, string idempotencyKey,
        string cardNumber = "4242424242424242")
    {
        var token = HtmlHelpers.ExtractAntiForgeryToken(reviewPageHtml);
        return client.PostAsync("/Checkout/PlaceOrder", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["addressId"] = addressId.ToString(),
            ["shippingMethodId"] = shippingMethodId.ToString(),
            ["idempotencyKey"] = idempotencyKey,
            ["cardNumber"] = cardNumber,
            ["cardholderName"] = "Jane Doe",
            ["expiryMonth"] = "12",
            ["expiryYear"] = "2030",
            ["cvv"] = "123",
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

    private static async Task AddToCartAsync(HttpClient client, int productId, int quantity = 1)
    {
        var homeHtml = await client.GetStringAsync("/");
        var csrfToken = HtmlHelpers.ExtractMetaCsrfToken(homeHtml);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/Add")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { ProductId = productId, ProductVariantId = (int?)null, Quantity = quantity }),
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
