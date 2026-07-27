using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Catalog;

[Collection(AuthTestCollection.Name)]
public class TaxRateAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public TaxRateAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_tax_rate_list()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/TaxRates/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("New tax rate");
    }

    [Fact]
    public async Task Customer_cannot_manage_tax_rates()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.taxrate.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/TaxRates/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task InventoryManager_cannot_manage_tax_rates()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"inventorymanager.taxrate.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "InventoryManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/TaxRates/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CatalogManager_can_manage_tax_rates()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"catalogmanager.taxrate.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CatalogManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/TaxRates/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New tax rate");
    }
}
