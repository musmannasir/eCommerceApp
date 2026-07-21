using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Catalog;

[Collection(AuthTestCollection.Name)]
public class HomePageBannerAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public HomePageBannerAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_banner_list()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/HomePageBanners/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("New banner");
    }

    [Fact]
    public async Task Customer_cannot_manage_banners()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.banner.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/HomePageBanners/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task InventoryManager_cannot_manage_banners()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"inventorymanager.banner.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "InventoryManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/HomePageBanners/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CatalogManager_can_manage_banners()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"catalogmanager.banner.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CatalogManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/HomePageBanners/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New banner");
    }
}
