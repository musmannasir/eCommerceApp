using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Storefront;

[Collection(AuthTestCollection.Name)]
public class OrderAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public OrderAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_order_queue()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Orders/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Order #");
    }

    [Fact]
    public async Task Customer_cannot_view_the_order_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.orderqueue.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Orders/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task CatalogManager_cannot_view_the_order_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"catalogmanager.orderqueue.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CatalogManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Orders/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task OrderManager_can_view_the_order_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.orderqueue.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Orders/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Order #");
    }

    [Fact]
    public async Task CustomerSupport_can_view_the_order_queue()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customersupport.orderqueue.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "CustomerSupport");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Orders/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Order #");
    }
}
