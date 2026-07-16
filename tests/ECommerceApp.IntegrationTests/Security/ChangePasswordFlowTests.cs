using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Security;

[Collection(AuthTestCollection.Name)]
public class ChangePasswordFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ChangePasswordFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Changing_the_password_via_the_MVC_form_succeeds_and_the_new_password_works()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"changepw.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Change", "Password");

        var cpPageResponse = await client.GetAsync("/Account/ChangePassword");
        var token = HtmlHelpers.ExtractAntiForgeryToken(await cpPageResponse.Content.ReadAsStringAsync());

        var response = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = "Str0ng!Passw0rd",
            ["NewPassword"] = "NewStr0ng!Passw0rd",
            ["ConfirmPassword"] = "NewStr0ng!Passw0rd",
            ["__RequestVerificationToken"] = token,
        }));

        response.IsSuccessStatusCode.Should().BeTrue();

        var freshClient = _fixture.Factory.CreateClient();
        var loginWithNewPassword = await freshClient.LoginViaFormAsync(email, "NewStr0ng!Passw0rd");
        loginWithNewPassword.IsSuccessStatusCode.Should().BeTrue();

        var loginWithOldPassword = await _fixture.Factory.CreateClient().LoginViaFormAsync(email, "Str0ng!Passw0rd");
        var oldPasswordBody = await loginWithOldPassword.Content.ReadAsStringAsync();
        oldPasswordBody.Should().Contain("Invalid email or password");
    }
}
