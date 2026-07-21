using System.Text.RegularExpressions;

namespace ECommerceApp.IntegrationTests.TestSupport;

public static partial class HtmlHelpers
{
    public static string ExtractAntiForgeryToken(string html)
    {
        var tagMatch = AntiForgeryInputTagRegex().Match(html);
        if (!tagMatch.Success)
        {
            throw new InvalidOperationException("Could not find the antiforgery input element in the response HTML.");
        }

        var valueMatch = ValueAttributeRegex().Match(tagMatch.Value);
        if (!valueMatch.Success)
        {
            throw new InvalidOperationException("Found the antiforgery input element but no value attribute.");
        }

        return valueMatch.Groups[1].Value;
    }

    /// <summary>Every page (via _Layout.cshtml) carries this meta tag for AJAX POSTs (Cart's Add/UpdateQuantity/Remove/Clear) that send the token as a header instead of a form field.</summary>
    public static string ExtractMetaCsrfToken(string html)
    {
        var match = MetaCsrfTokenRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not find the csrf-token meta tag in the response HTML.");
        }

        return match.Groups[1].Value;
    }

    [GeneratedRegex("""<input[^>]*name="__RequestVerificationToken"[^>]*>""")]
    private static partial Regex AntiForgeryInputTagRegex();

    [GeneratedRegex("value=\"([^\"]*)\"")]
    private static partial Regex ValueAttributeRegex();

    [GeneratedRegex("""<meta name="csrf-token" content="([^"]*)" />""")]
    private static partial Regex MetaCsrfTokenRegex();
}
