using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests;

/// <summary>
/// Confirms the whole application boots end-to-end through the real ASP.NET Core
/// pipeline (DI container, middleware, routing). Originally these tests ran
/// against a placeholder, unreachable connection string, since nothing on the
/// request paths under test touched the database. That stopped being true at
/// Milestone 4.1: the public layout's category nav (rendered on every page,
/// including the 404 and login pages) and the home page itself now query real
/// catalog data - so this class now shares the same real test-database fixture
/// every other integration test class uses.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ApplicationStartupTests
{
    private readonly AuthTestFixture _fixture;

    public ApplicationStartupTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Home_page_returns_success()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Health_live_endpoint_reports_healthy_without_touching_the_database()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Health_ready_endpoint_reports_healthy_against_the_real_test_database()
    {
        // Milestone 17.3 added a bounded timeout to SqlServerHealthCheck - this
        // confirms the happy path still reports healthy well within it against a
        // real, reachable SQL Server, not just that the timeout exists in isolation.
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_route_renders_the_branded_not_found_page()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Anonymous_admin_area_request_redirects_to_login_rather_than_the_dashboard()
    {
        // As of Milestone 1 the Admin Area requires authentication; see
        // ECommerceApp.IntegrationTests.Security.AdminAreaAuthorizationTests for
        // the full authorization matrix against the real test database.
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Admin dashboard");
    }
}
