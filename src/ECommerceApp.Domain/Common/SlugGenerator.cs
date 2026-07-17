using System.Text;
using System.Text.RegularExpressions;

namespace ECommerceApp.Domain.Common;

public static partial class SlugGenerator
{
    public static string Generate(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var withoutDiacritics = RemoveDiacritics(normalized);
        var hyphenated = NonAlphanumericRegex().Replace(withoutDiacritics, "-");
        var collapsed = MultipleHyphensRegex().Replace(hyphenated, "-");
        return collapsed.Trim('-');
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
