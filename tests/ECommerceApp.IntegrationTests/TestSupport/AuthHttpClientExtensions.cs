namespace ECommerceApp.IntegrationTests.TestSupport;

public static class AuthHttpClientExtensions
{
    /// <summary>Performs a real MVC form login (GET for the antiforgery token, then POST), leaving the auth cookie on the client.</summary>
    public static async Task<HttpResponseMessage> LoginViaFormAsync(
        this HttpClient client, string email, string password, string? returnUrl = null)
    {
        var loginPageResponse = await client.GetAsync("/Account/Login" + (returnUrl is null ? "" : $"?returnUrl={Uri.EscapeDataString(returnUrl)}"));
        var loginPageHtml = await loginPageResponse.Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(loginPageHtml);

        var formValues = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        };
        if (returnUrl is not null)
        {
            formValues["ReturnUrl"] = returnUrl;
        }

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(formValues));
    }

    public static async Task<HttpResponseMessage> RegisterViaFormAsync(
        this HttpClient client, string email, string password, string firstName, string lastName)
    {
        var registerPageResponse = await client.GetAsync("/Account/Register");
        var registerPageHtml = await registerPageResponse.Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(registerPageHtml);

        var formValues = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password,
            ["FirstName"] = firstName,
            ["LastName"] = lastName,
            ["__RequestVerificationToken"] = token,
        };

        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(formValues));
    }
}
