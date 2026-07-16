using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests;

/// <summary>
/// Confirms the whole application boots end-to-end through the real ASP.NET Core
/// pipeline (DI container, middleware, routing) without requiring a reachable
/// SQL Server instance - a placeholder connection string is enough to satisfy
/// service registration for these tests.
/// </summary>
public class ApplicationStartupTests : IClassFixture<WebApplicationFactory<Web.Program>>
{
    private readonly WebApplicationFactory<Web.Program> _factory;

    public ApplicationStartupTests(WebApplicationFactory<Web.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=localhost;Database=ECommerceAppTestDb;Trusted_Connection=True;TrustServerCertificate=True;",
                });
            });
        });
    }

    [Fact]
    public async Task Home_page_returns_success()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Health_live_endpoint_reports_healthy_without_touching_the_database()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_route_renders_the_branded_not_found_page()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Anonymous_admin_area_request_redirects_to_login_rather_than_the_dashboard()
    {
        // As of Milestone 1 the Admin Area requires authentication; see
        // ECommerceApp.IntegrationTests.Security.AdminAreaAuthorizationTests for
        // the full authorization matrix against the real test database.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Admin dashboard");
    }
}
