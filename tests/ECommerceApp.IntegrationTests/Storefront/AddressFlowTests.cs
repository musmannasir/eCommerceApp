using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the Address book's classic MVC forms over real HTTP against the
/// real SQL Server test database (Milestone 8.1) - proves the [Authorize]
/// gate redirects an anonymous visitor to login, and that ownership isolation
/// actually holds over a real HTTP round trip (not just in-process), the same
/// rigor CartFlowTests/WishlistFlowTests applied to their own new surface area.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class AddressFlowTests
{
    private readonly AuthTestFixture _fixture;

    public AddressFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_anonymous_visitor_loading_the_addresses_page_is_redirected_to_login()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Addresses");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in");
    }

    [Fact]
    public async Task A_signed_in_customer_can_create_an_address_and_see_it_listed()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"address.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Addr", "Ess");

        await CreateAddressAsync(client, fullName: "Jane Doe", label: "Home");

        var pageResponse = await client.GetAsync("/Addresses");
        var pageBody = await pageResponse.Content.ReadAsStringAsync();

        pageResponse.IsSuccessStatusCode.Should().BeTrue();
        pageBody.Should().Contain("Jane Doe");
        pageBody.Should().Contain("Default");
    }

    [Fact]
    public async Task A_customer_cannot_view_edit_or_delete_another_customers_address()
    {
        var ownerClient = _fixture.Factory.CreateClient();
        var ownerEmail = $"owner.{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterViaFormAsync(ownerEmail, "Str0ng!Passw0rd", "Owner", "One");
        var addressId = await CreateAddressAsync(ownerClient, fullName: "Owner Address", label: "Home");

        var otherClient = _fixture.Factory.CreateClient();
        var otherEmail = $"other.{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterViaFormAsync(otherEmail, "Str0ng!Passw0rd", "Other", "Two");

        var editPageResponse = await otherClient.GetAsync($"/Addresses/Edit/{addressId}");
        ((int)editPageResponse.StatusCode).Should().Be(404);

        // Any antiforgery token from this user's own session is valid for any
        // action on the site - it's tied to the session, not the route - so
        // it's fine to source it from a page otherClient can actually load.
        var createPageHtml = await otherClient.GetStringAsync("/Addresses/Create");
        var token = HtmlHelpers.ExtractAntiForgeryToken(createPageHtml);

        var deleteResponse = await otherClient.PostAsync($"/Addresses/Delete/{addressId}", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        // Delete redirects back to Index regardless of ownership (TempData
        // error, not a 404) - so assert the address still exists for its
        // real owner instead of asserting on the redirect response itself.
        deleteResponse.IsSuccessStatusCode.Should().BeTrue();
        var ownerPageBody = await ownerClient.GetStringAsync("/Addresses");
        ownerPageBody.Should().Contain("Owner Address");
    }

    private static async Task<int> CreateAddressAsync(HttpClient client, string fullName, string label)
    {
        var createPageResponse = await client.GetAsync("/Addresses/Create");
        var createPageHtml = await createPageResponse.Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(createPageHtml);

        var formValues = new Dictionary<string, string>
        {
            ["Label"] = label,
            ["FullName"] = fullName,
            ["Phone"] = "555-0100",
            ["Line1"] = "123 Main St",
            ["City"] = "Springfield",
            ["PostalCode"] = "90210",
            ["CountryCode"] = "US",
            ["__RequestVerificationToken"] = token,
        };

        await client.PostAsync("/Addresses/Create", new FormUrlEncodedContent(formValues));

        var indexHtml = await client.GetStringAsync("/Addresses");
        var editHrefMatch = System.Text.RegularExpressions.Regex.Match(indexHtml, "/Addresses/Edit/(\\d+)");
        return int.Parse(editHrefMatch.Groups[1].Value);
    }
}
