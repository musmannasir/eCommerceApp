using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommerceApp.IntegrationTests.Catalog;

/// <summary>
/// Full admin-UI product flow against the real SQL Server test database. This is deliberately
/// end-to-end (not against the InMemory-backed unit test harness): a bug where AddVariantAsync's
/// re-query was missing the Include() chain MapVariant needs only threw once EF Core couldn't
/// silently fix up the navigation from an already-tracked entity in the same DbContext - the
/// InMemory provider's more lenient tracking masked it in Infrastructure.Tests entirely.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ProductAdminFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ProductAdminFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Creating_a_complete_product_through_the_admin_UI_succeeds()
    {
        // Redirects are disabled and followed manually: HttpResponseMessage.RequestMessage
        // .RequestUri does not reliably reflect the final URL after auto-redirect through
        // WebApplicationFactory's in-process TestServer, so the Location header - read directly
        // off the redirect response - is the only reliable way to recover a new entity's id.
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Category. DELETE doesn't reset IDENTITY seeds, so across repeated local test runs
        // against the same dev/test database "category id 1" won't reliably exist - the actual
        // created id must be scraped from the list page rather than assumed.
        var categoryToken = HtmlHelpers.ExtractAntiForgeryToken(await GetAsync(client, "/Admin/Categories/Create"));
        await PostExpectRedirectAsync(client, "/Admin/Categories/Create", new()
        {
            ["Name"] = $"Category-{suffix}",
            ["DisplayOrder"] = "0",
            ["IsActive"] = "true",
            ["__RequestVerificationToken"] = categoryToken,
        });

        var categoryIndexHtml = await GetAsync(client, $"/Admin/Categories/Index?search=Category-{suffix}");
        var categoryIdMatch = System.Text.RegularExpressions.Regex.Match(categoryIndexHtml, @"/Admin/Categories/Edit/(\d+)");
        categoryIdMatch.Success.Should().BeTrue(because: "the just-created category should appear in the filtered list");
        var categoryId = categoryIdMatch.Groups[1].Value;

        // Attribute + value
        var attrToken = HtmlHelpers.ExtractAntiForgeryToken(await GetAsync(client, "/Admin/ProductAttributes/Index"));
        await PostExpectRedirectAsync(client, "/Admin/ProductAttributes/CreateAttribute", new()
        {
            ["Name"] = $"Attr-{suffix}",
            ["__RequestVerificationToken"] = attrToken,
        });

        var attrIndexHtml = await GetAsync(client, "/Admin/ProductAttributes/Index");
        var attributeId = System.Text.RegularExpressions.Regex.Match(attrIndexHtml, "name=\"ProductAttributeId\" value=\"(\\d+)\"").Groups[1].Value;
        var valueToken = HtmlHelpers.ExtractAntiForgeryToken(attrIndexHtml);
        await PostExpectRedirectAsync(client, "/Admin/ProductAttributes/CreateValue", new()
        {
            ["ProductAttributeId"] = attributeId,
            ["Value"] = $"Value-{suffix}",
            ["__RequestVerificationToken"] = valueToken,
        });

        // Product
        var createToken = HtmlHelpers.ExtractAntiForgeryToken(await GetAsync(client, "/Admin/Products/Create"));
        var productLocation = await PostExpectRedirectAsync(client, "/Admin/Products/Create", new()
        {
            ["Name"] = $"Product-{suffix}",
            ["CategoryId"] = categoryId,
            ["BaseSKU"] = $"SKU-{suffix}",
            ["CostPrice"] = "5.00",
            ["SellingPrice"] = "19.99",
            ["TaxCategory"] = "Standard",
            ["IsTaxable"] = "true",
            ["IsActive"] = "true",
            ["__RequestVerificationToken"] = createToken,
        });
        var productId = productLocation.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries)[^1];

        // Variant - this is the operation that previously threw a 500 in manual testing.
        // The "av-{id}" checkboxes only exist on the product Edit page's Variants tab
        // (rendered per attribute value), not on the ProductAttributes index page.
        var editHtml = await GetAsync(client, $"/Admin/Products/Edit/{productId}");
        var valueIdMatch = System.Text.RegularExpressions.Regex.Match(
            editHtml, $"id=\"av-(\\d+)\"[^>]*/>\\s*<label[^>]*>Value-{suffix}</label>");
        valueIdMatch.Success.Should().BeTrue(because: "the variant tab should list the attribute value we just created");
        var valueId = valueIdMatch.Groups[1].Value;

        var variantToken = HtmlHelpers.ExtractAntiForgeryToken(editHtml);
        await PostExpectRedirectAsync(client, "/Admin/Products/AddVariant", new()
        {
            ["ProductId"] = productId,
            ["SKU"] = $"SKU-{suffix}-V1",
            ["AttributeValueIds"] = valueId,
            ["__RequestVerificationToken"] = variantToken,
        });

        var afterVariantHtml = await GetAsync(client, $"/Admin/Products/Edit/{productId}");
        afterVariantHtml.Should().Contain($"SKU-{suffix}-V1");

        // Specification and tag, to round out "a complete product".
        var specToken = HtmlHelpers.ExtractAntiForgeryToken(afterVariantHtml);
        await PostExpectRedirectAsync(client, "/Admin/Products/AddSpecification", new()
        {
            ["ProductId"] = productId,
            ["Name"] = "Weight",
            ["Value"] = "200g",
            ["__RequestVerificationToken"] = specToken,
        });

        var tagToken = HtmlHelpers.ExtractAntiForgeryToken(await GetAsync(client, $"/Admin/Products/Edit/{productId}"));
        await PostExpectRedirectAsync(client, "/Admin/Products/AddTag", new()
        {
            ["ProductId"] = productId,
            ["TagName"] = "Featured",
            ["__RequestVerificationToken"] = tagToken,
        });

        var completeHtml = await GetAsync(client, $"/Admin/Products/Edit/{productId}");
        completeHtml.Should().Contain("200g").And.Contain("Featured");
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.IsSuccessStatusCode.Should().BeTrue(because: $"GET {url} should succeed");
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Posts a form and asserts the action redirected (its normal success path), returning the Location.</summary>
    private static async Task<Uri> PostExpectRedirectAsync(HttpClient client, string url, Dictionary<string, string> form)
    {
        var response = await client.PostAsync(url, new FormUrlEncodedContent(form));
        ((int)response.StatusCode).Should().BeInRange(300, 399, because: await SafeBodyAsync(response));
        return response.Headers.Location!;
    }

    private static async Task<string> SafeBodyAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();
}
