using System.Text;

namespace ECommerceApp.Web.Common;

/// <summary>
/// Hand-rolled rather than a third-party CSV library (Milestone 14.3) - no
/// CSV/export dependency exists anywhere in this app, and the data being
/// exported here is always simple flat rows, so a small, correctly-escaping
/// writer covers it without adding a new dependency for one milestone.
/// </summary>
public static class CsvExport
{
    public static byte[] BuildCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeField)));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeField)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeField(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
