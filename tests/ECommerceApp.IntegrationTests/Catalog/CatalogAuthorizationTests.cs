using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Catalog;

[Collection(AuthTestCollection.Name)]
public class CatalogAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public CatalogAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_catalog()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Categories/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("New category");
    }

    [Fact]
    public async Task Customer_cannot_manage_the_catalog()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.catalog.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Categories/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CatalogManager_can_manage_the_catalog()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"catalogmanager.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CatalogManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Categories/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New category");
    }

    [Fact]
    public async Task SuperAdmin_can_manage_the_catalog()
    {
        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var response = await client.GetAsync("/Admin/Products/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New product");
    }
}
