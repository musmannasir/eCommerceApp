using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Catalog;

[Collection(AuthTestCollection.Name)]
public class ProductAttributeAdminFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ProductAttributeAdminFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Creating_an_attribute_then_a_value_for_it_through_the_admin_UI_succeeds()
    {
        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var indexHtml1 = await (await client.GetAsync("/Admin/ProductAttributes/Index")).Content.ReadAsStringAsync();
        var token1 = HtmlHelpers.ExtractAntiForgeryToken(indexHtml1);

        var createAttrResponse = await client.PostAsync("/Admin/ProductAttributes/CreateAttribute", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "DiagColor",
            ["__RequestVerificationToken"] = token1,
        }));
        createAttrResponse.IsSuccessStatusCode.Should().BeTrue();

        var indexHtml2 = await (await client.GetAsync("/Admin/ProductAttributes/Index")).Content.ReadAsStringAsync();
        indexHtml2.Should().Contain("DiagColor");

        // Find the id assigned to the just-created attribute from its rendered hidden input.
        var idMatch = System.Text.RegularExpressions.Regex.Match(indexHtml2, "name=\"ProductAttributeId\" value=\"(\\d+)\"");
        idMatch.Success.Should().BeTrue();
        var attributeId = idMatch.Groups[1].Value;

        var token2 = HtmlHelpers.ExtractAntiForgeryToken(indexHtml2);
        var createValueResponse = await client.PostAsync("/Admin/ProductAttributes/CreateValue", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ProductAttributeId"] = attributeId,
            ["Value"] = "DiagRed",
            ["__RequestVerificationToken"] = token2,
        }));
        createValueResponse.IsSuccessStatusCode.Should().BeTrue();

        var indexHtml3 = await (await client.GetAsync("/Admin/ProductAttributes/Index")).Content.ReadAsStringAsync();
        indexHtml3.Should().Contain("DiagRed");
    }
}
