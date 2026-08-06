using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Security;

/// <summary>
/// Milestone 17.1 - security response headers and the CORS policy, both
/// explicitly flagged as deliberately not built in `Security.md` through
/// Milestone 16.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class SecurityHardeningFlowTests
{
    private readonly AuthTestFixture _fixture;

    public SecurityHardeningFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Every_response_carries_the_hardening_headers()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/");

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("strict-origin-when-cross-origin");
        response.Headers.GetValues("Permissions-Policy").Should().ContainSingle(v => v.Contains("geolocation=()"));
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle(v =>
            v.Contains("default-src 'self'") && v.Contains("frame-ancestors 'none'") && v.Contains("object-src 'none'"));
    }

    [Fact]
    public async Task The_hardening_headers_are_present_on_api_responses_too()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/nonexistent-route-for-header-check");

        response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task A_disallowed_cross_origin_request_gets_no_CORS_header()
    {
        var client = _fixture.Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "https://not-on-the-allow-list.example");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task An_allow_listed_cross_origin_request_gets_the_matching_CORS_header()
    {
        var client = _fixture.Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "https://allowed-test-origin.example");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle("https://allowed-test-origin.example");
    }
}
