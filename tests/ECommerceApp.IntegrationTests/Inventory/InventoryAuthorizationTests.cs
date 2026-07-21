using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Inventory;

[Collection(AuthTestCollection.Name)]
public class InventoryAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public InventoryAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_inventory_overview()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Inventory/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Record opening stock");
    }

    [Fact]
    public async Task Customer_cannot_manage_inventory()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.inventory.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Inventory/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CatalogManager_cannot_manage_inventory()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"catalogmanager.inventory.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CatalogManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Inventory/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task InventoryManager_can_manage_inventory()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"inventorymanager.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "InventoryManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Inventory/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Record opening stock");
    }

    [Fact]
    public async Task SuperAdmin_can_manage_warehouses()
    {
        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var response = await client.GetAsync("/Admin/Warehouses/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New warehouse");
    }
}
