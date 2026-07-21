using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommerceApp.IntegrationTests.Inventory;

[Collection(AuthTestCollection.Name)]
public class SupplierAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public SupplierAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_supplier_list()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Suppliers/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("New supplier");
    }

    [Fact]
    public async Task Customer_cannot_manage_suppliers()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.supplier.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Suppliers/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CatalogManager_cannot_manage_suppliers()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"catalogmanager.supplier.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CatalogManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Suppliers/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task InventoryManager_can_manage_suppliers()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"inventorymanager.supplier.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "InventoryManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Suppliers/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New supplier");
    }

    [Fact]
    public async Task SuperAdmin_can_create_a_supplier_end_to_end()
    {
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var createPageHtml = await (await client.GetAsync("/Admin/Suppliers/Create")).Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(createPageHtml);
        var code = $"SUP{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var response = await client.PostAsync("/Admin/Suppliers/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Integration Test Supplier",
            ["Code"] = code,
            ["IsActive"] = "true",
        }));

        ((int)response.StatusCode).Should().BeInRange(300, 399);
        response.Headers.Location!.ToString().Should().Contain("/Admin/Suppliers");
    }
}
