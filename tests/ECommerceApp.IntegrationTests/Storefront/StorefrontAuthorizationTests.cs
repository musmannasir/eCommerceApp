using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Milestone 4.1's one named test requirement: unauthorized catalog-admin
/// access from storefront context - a customer browsing the public site
/// must never be able to reach admin catalog management.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class StorefrontAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public StorefrontAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task The_public_home_page_loads_for_an_anonymous_visitor_with_no_admin_content()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().NotContain("/Admin/").And.NotContain("New product");
    }

    [Fact]
    public async Task An_anonymous_visitor_browsing_the_storefront_cannot_reach_catalog_admin()
    {
        var client = _fixture.Factory.CreateClient();
        await client.GetAsync("/");

        var response = await client.GetAsync("/Admin/Products/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("New product");
    }

    [Fact]
    public async Task A_customer_browsing_the_storefront_cannot_reach_catalog_admin()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.storefront.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");
        await client.GetAsync("/");

        var response = await client.GetAsync("/Admin/Categories/Index");

        ((int)response.StatusCode).Should().Be(403);
    }
}
