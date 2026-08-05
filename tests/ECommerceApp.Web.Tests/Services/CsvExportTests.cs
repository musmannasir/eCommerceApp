using System.Text;
using ECommerceApp.Web.Common;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.Web.Tests.Services;

public class CsvExportTests
{
    [Fact]
    public void BuildCsv_writes_a_header_row_followed_by_each_data_row()
    {
        var csv = Encoding.UTF8.GetString(CsvExport.BuildCsv(
            new[] { "Name", "Amount" },
            new[] { new[] { "Widget", "10.00" }, new[] { "Gadget", "20.00" } }));

        csv.Should().Be("Name,Amount\r\nWidget,10.00\r\nGadget,20.00\r\n");
    }

    [Fact]
    public void BuildCsv_quotes_a_field_containing_a_comma()
    {
        var csv = Encoding.UTF8.GetString(CsvExport.BuildCsv(new[] { "Name" }, new[] { new[] { "Widget, Deluxe" } }));

        csv.Should().Contain("\"Widget, Deluxe\"");
    }

    [Fact]
    public void BuildCsv_escapes_an_embedded_quote_by_doubling_it()
    {
        var csv = Encoding.UTF8.GetString(CsvExport.BuildCsv(new[] { "Name" }, new[] { new[] { "18\" Widget" } }));

        csv.Should().Contain("\"18\"\"Widget\"".Replace("Widget", " Widget"));
    }

    [Fact]
    public void BuildCsv_quotes_a_field_containing_a_newline()
    {
        var csv = Encoding.UTF8.GetString(CsvExport.BuildCsv(new[] { "Name" }, new[] { new[] { "Line1\nLine2" } }));

        csv.Should().Contain("\"Line1\nLine2\"");
    }

    [Fact]
    public void BuildCsv_leaves_a_plain_field_unquoted()
    {
        var csv = Encoding.UTF8.GetString(CsvExport.BuildCsv(new[] { "Name" }, new[] { new[] { "Widget" } }));

        csv.Should().Contain("Widget").And.NotContain("\"Widget\"");
    }
}
