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

    [GeneratedRegex("""<input[^>]*name="__RequestVerificationToken"[^>]*>""")]
    private static partial Regex AntiForgeryInputTagRegex();

    [GeneratedRegex("value=\"([^\"]*)\"")]
    private static partial Regex ValueAttributeRegex();
}
