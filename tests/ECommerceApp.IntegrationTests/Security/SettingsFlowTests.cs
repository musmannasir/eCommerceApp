using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Security;

/// <summary>
/// Milestone 16.3 - the single admin-editable store settings row that used
/// to live only in appsettings.json's static "Store" section. Tests here
/// deliberately keep DefaultTaxCountryCode/DefaultShippingCountryCode at
/// "PK"/"" in every submitted form - EstimatedTaxFlowTests/EstimatedShippingFlowTests
/// share this same collection-wide database and rely on that default
/// jurisdiction staying put.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class SettingsFlowTests
{
    private readonly AuthTestFixture _fixture;

    public SettingsFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_settings()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Settings/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Store settings");
    }

    [Fact]
    public async Task Customer_cannot_view_settings()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.settings.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Settings/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task OrderManager_cannot_view_settings()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.settings.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Settings/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Admin_can_view_the_settings_page()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.settings.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Settings/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Store settings");
    }

    [Fact]
    public async Task SuperAdmin_can_update_settings_and_the_storefront_reflects_it_immediately()
    {
        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var settingsPageHtml = await client.GetStringAsync("/Admin/Settings/Index");
        var token = HtmlHelpers.ExtractAntiForgeryToken(settingsPageHtml);
        var rowVersion = HtmlHelpers.ExtractInputValue(settingsPageHtml, "RowVersion");
        var newStoreName = $"Updated Store {Guid.NewGuid():N}";

        var response = await client.PostAsync("/Admin/Settings/Index", new FormUrlEncodedContent(FormFor(
            token, rowVersion, storeName: newStoreName)));

        response.IsSuccessStatusCode.Should().BeTrue();

        var homePageHtml = await _fixture.Factory.CreateClient().GetStringAsync("/");
        homePageHtml.Should().Contain(newStoreName);
    }

    [Fact]
    public async Task Updating_settings_with_a_stale_RowVersion_returns_a_conflict_error()
    {
        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var firstLoadHtml = await client.GetStringAsync("/Admin/Settings/Index");
        var staleToken = HtmlHelpers.ExtractAntiForgeryToken(firstLoadHtml);
        var staleRowVersion = HtmlHelpers.ExtractInputValue(firstLoadHtml, "RowVersion");

        // Someone else saves a change first, advancing the row's RowVersion
        // past what this client's form is still holding.
        var firstSave = await client.PostAsync("/Admin/Settings/Index", new FormUrlEncodedContent(FormFor(
            staleToken, staleRowVersion, storeName: $"First save {Guid.NewGuid():N}")));
        firstSave.IsSuccessStatusCode.Should().BeTrue();

        var conflictingResponse = await client.PostAsync("/Admin/Settings/Index", new FormUrlEncodedContent(FormFor(
            staleToken, staleRowVersion, storeName: $"Stale save {Guid.NewGuid():N}")));
        var conflictingBody = await conflictingResponse.Content.ReadAsStringAsync();

        conflictingResponse.IsSuccessStatusCode.Should().BeTrue();
        conflictingBody.Should().Contain("Settings were changed by someone else");
    }

    private static Dictionary<string, string> FormFor(string antiForgeryToken, string rowVersion, string storeName) => new()
    {
        ["__RequestVerificationToken"] = antiForgeryToken,
        ["StoreName"] = storeName,
        ["Currency"] = "PKR",
        ["DefaultCountry"] = "Pakistan",
        ["RecentlyViewedMaxItems"] = "10",
        ["DefaultTaxCountryCode"] = "PK",
        ["DefaultTaxRegionCode"] = "",
        ["DefaultShippingCountryCode"] = "PK",
        ["DefaultShippingRegionCode"] = "",
        ["RowVersion"] = rowVersion,
    };
}
