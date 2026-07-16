using System.Net.Http.Headers;
using System.Net.Http.Json;
using ECommerceApp.Application.Auth.Models;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Security;

[Collection(AuthTestCollection.Name)]
public class ApiAuthEndpointTests
{
    private readonly AuthTestFixture _fixture;

    public ApiAuthEndpointTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_then_calling_me_with_the_issued_access_token_succeeds()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"api.register.{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new RegisterRequest(email, "Str0ng!Passw0rd", "Api", "User"));
        registerResponse.EnsureSuccessStatusCode();
        var loginResult = await registerResponse.Content.ReadFromJsonAsync<LoginResult>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.Tokens.AccessToken);
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();

        meResponse.IsSuccessStatusCode.Should().BeTrue();
        me!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Calling_me_without_a_bearer_token_is_unauthorized()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        ((int)response.StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_a_conflict()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"api.duplicate.{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, "Str0ng!Passw0rd", "Api", "User");

        await client.PostAsJsonAsync("/api/v1/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        ((int)secondResponse.StatusCode).Should().Be(409);
    }

    [Fact]
    public async Task Refresh_endpoint_rotates_the_refresh_token()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"api.refresh.{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new RegisterRequest(email, "Str0ng!Passw0rd", "Api", "User"));
        var initialTokens = (await registerResponse.Content.ReadFromJsonAsync<LoginResult>())!.Tokens;

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = initialTokens.RefreshToken });
        var rotatedTokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokens>();

        refreshResponse.IsSuccessStatusCode.Should().BeTrue();
        rotatedTokens!.RefreshToken.Should().NotBe(initialTokens.RefreshToken);

        var reuseResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = initialTokens.RefreshToken });
        ((int)reuseResponse.StatusCode).Should().Be(401);
    }
}
