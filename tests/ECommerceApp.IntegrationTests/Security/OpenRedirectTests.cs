using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommerceApp.IntegrationTests.Security;

[Collection(AuthTestCollection.Name)]
public class OpenRedirectTests
{
    private readonly AuthTestFixture _fixture;

    public OpenRedirectTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_with_an_external_returnUrl_does_not_redirect_off_site()
    {
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"redirect.{Guid.NewGuid():N}@example.com";

        var registerClient = _fixture.Factory.CreateClient();
        await registerClient.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Redirect", "Test");

        var response = await client.LoginViaFormAsync(email, "Str0ng!Passw0rd", returnUrl: "https://evil.example.com/steal-session");

        ((int)response.StatusCode).Should().BeInRange(300, 399);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().NotContain("evil.example.com");
    }
}
