using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Security;

[Collection(AuthTestCollection.Name)]
public class AdminAreaAuthorizationTests
{
    private readonly AuthTestFixture _fixture;

    public AdminAreaAuthorizationTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_dashboard()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Admin dashboard");
    }

    [Fact]
    public async Task Customer_cannot_access_the_admin_area()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).Should().Be(403);
        body.Should().Contain("Access denied");
    }

    [Fact]
    public async Task SuperAdmin_can_access_the_admin_dashboard()
    {
        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Admin dashboard");
    }
}
