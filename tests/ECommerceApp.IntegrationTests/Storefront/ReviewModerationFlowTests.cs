using System.Text.RegularExpressions;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the review report -> admin moderation queue -> Dismiss/Remove flow
/// over real HTTP (Milestone 12.2). Admin-area authorization mirrors
/// OrderAuthorizationTests' shape exactly, since the moderation queue reuses
/// Policies.CanManageOrders rather than a new dedicated policy.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ReviewModerationFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ReviewModerationFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_moderation_queue()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Reviews/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Review Moderation");
    }

    [Fact]
    public async Task Customer_cannot_view_the_moderation_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.moderation.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Reviews/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CustomerSupport_can_view_the_moderation_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customersupport.moderation.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CustomerSupport");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Reviews/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Review Moderation");
    }

    [Fact]
    public async Task Reporting_a_review_surfaces_it_in_the_moderation_queue_and_dismissing_clears_it()
    {
        var product = await SeedProductAsync();

        var authorClient = _fixture.Factory.CreateClient();
        var authorEmail = $"author.{Guid.NewGuid():N}@example.com";
        await authorClient.RegisterViaFormAsync(authorEmail, "Str0ng!Passw0rd", "Review", "Author");
        var reviewId = await SubmitReviewAsync(authorClient, product.Slug, product.Id, "Not great.");

        var reporterClient = _fixture.Factory.CreateClient();
        var reporterEmail = $"reporter.{Guid.NewGuid():N}@example.com";
        await reporterClient.RegisterViaFormAsync(reporterEmail, "Str0ng!Passw0rd", "Review", "Reporter");
        await ReportReviewAsync(reporterClient, product.Slug, reviewId);

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.moderation.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "CustomerSupport");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var queueHtml = await adminClient.GetStringAsync("/Admin/Reviews/Index");
        queueHtml.Should().Contain("Not great.").And.Contain("1 report");

        var dismissToken = HtmlHelpers.ExtractAntiForgeryToken(queueHtml);
        var dismissResponse = await adminClient.PostAsync("/Admin/Reviews/Dismiss", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["reviewId"] = reviewId.ToString(), ["__RequestVerificationToken"] = dismissToken }));
        var afterDismissHtml = await dismissResponse.Content.ReadAsStringAsync();

        afterDismissHtml.Should().Contain("Report(s) dismissed").And.NotContain("Not great.");

        var productPageHtml = await authorClient.GetStringAsync($"/Product/{product.Slug}");
        productPageHtml.Should().Contain("Not great.");
    }

    [Fact]
    public async Task Removing_a_review_hides_it_from_the_product_page()
    {
        var product = await SeedProductAsync();

        var authorClient = _fixture.Factory.CreateClient();
        var authorEmail = $"removeauthor.{Guid.NewGuid():N}@example.com";
        await authorClient.RegisterViaFormAsync(authorEmail, "Str0ng!Passw0rd", "Remove", "Author");
        var reviewId = await SubmitReviewAsync(authorClient, product.Slug, product.Id, "Abusive content.");

        var reporterClient = _fixture.Factory.CreateClient();
        var reporterEmail = $"removereporter.{Guid.NewGuid():N}@example.com";
        await reporterClient.RegisterViaFormAsync(reporterEmail, "Str0ng!Passw0rd", "Remove", "Reporter");
        await ReportReviewAsync(reporterClient, product.Slug, reviewId);

        var adminClient = _fixture.Factory.CreateClient();
        var adminEmail = $"admin.remove.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(adminEmail, "Str0ng!Passw0rd", "CustomerSupport");
        await adminClient.LoginViaFormAsync(adminEmail, "Str0ng!Passw0rd");

        var queueHtml = await adminClient.GetStringAsync("/Admin/Reviews/Index");
        var removeToken = HtmlHelpers.ExtractAntiForgeryToken(queueHtml);
        await adminClient.PostAsync("/Admin/Reviews/Remove", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["reviewId"] = reviewId.ToString(), ["__RequestVerificationToken"] = removeToken }));

        var productPageHtml = await authorClient.GetStringAsync($"/Product/{product.Slug}");
        productPageHtml.Should().NotContain("Abusive content.").And.Contain("No reviews yet");
    }

    [Fact]
    public async Task Reporting_the_same_review_twice_by_the_same_reporter_is_rejected()
    {
        var product = await SeedProductAsync();

        var authorClient = _fixture.Factory.CreateClient();
        var authorEmail = $"dupauthor.{Guid.NewGuid():N}@example.com";
        await authorClient.RegisterViaFormAsync(authorEmail, "Str0ng!Passw0rd", "Dup", "Author");
        var reviewId = await SubmitReviewAsync(authorClient, product.Slug, product.Id, "Body.");

        var reporterClient = _fixture.Factory.CreateClient();
        var reporterEmail = $"dupreporter.{Guid.NewGuid():N}@example.com";
        await reporterClient.RegisterViaFormAsync(reporterEmail, "Str0ng!Passw0rd", "Dup", "Reporter");
        await ReportReviewAsync(reporterClient, product.Slug, reviewId);
        var secondReportBody = await ReportReviewAsync(reporterClient, product.Slug, reviewId);

        secondReportBody.Should().Contain("You have already reported this review.");
    }

    private static async Task<int> SubmitReviewAsync(HttpClient client, string slug, int productId, string body)
    {
        var detailsHtml = await client.GetStringAsync($"/Product/{slug}");
        var token = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);

        var formValues = new Dictionary<string, string>
        {
            ["ProductId"] = productId.ToString(),
            ["Rating"] = "2",
            ["Body"] = body,
            ["__RequestVerificationToken"] = token,
        };
        var response = await client.PostAsync($"/Product/{slug}/Review", new FormUrlEncodedContent(formValues));
        var afterSubmitHtml = await response.Content.ReadAsStringAsync();

        return int.Parse(Regex.Match(afterSubmitHtml, "name=\"ReviewId\" value=\"(\\d+)\"").Groups[1].Value);
    }

    private static async Task<string> ReportReviewAsync(HttpClient client, string slug, int reviewId)
    {
        var detailsHtml = await client.GetStringAsync($"/Product/{slug}");
        var token = HtmlHelpers.ExtractAntiForgeryToken(detailsHtml);

        var formValues = new Dictionary<string, string>
        {
            ["ReviewId"] = reviewId.ToString(),
            ["Reason"] = "Spam",
            ["__RequestVerificationToken"] = token,
        };
        var response = await client.PostAsync($"/Product/{slug}/Review/{reviewId}/Report", new FormUrlEncodedContent(formValues));
        return await response.Content.ReadAsStringAsync();
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
}
