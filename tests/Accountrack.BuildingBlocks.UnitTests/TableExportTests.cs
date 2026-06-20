using Accountrack.SharedKernel.Export;
using Accountrack.Web.Common.Export;
using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Accountrack.BuildingBlocks.UnitTests;

public class TableExportTests
{
    private static TabularData Sample() => TabularData.From(
        new[] { "Code", "Name", "Amount" },
        new[]
        {
            (IReadOnlyList<string?>)new string?[] { "C1", "Acme, Inc.", "1000" },
            new string?[] { "C2", "Globex", null },
        });

    [Fact]
    public void Csv_matches_the_shared_writer()
    {
        var bytes = TableExport.Csv(Sample());
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        text.Should().StartWith("Code,Name,Amount\r\n");
        text.Should().Contain("\"Acme, Inc.\"");
    }

    [Fact]
    public void Xlsx_is_a_valid_workbook_with_header_and_rows()
    {
        var bytes = TableExport.Xlsx(Sample(), "Customers");

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        ws.Cell(1, 1).GetString().Should().Be("Code");
        ws.Cell(1, 3).GetString().Should().Be("Amount");
        ws.Cell(2, 2).GetString().Should().Be("Acme, Inc.");
        ws.Cell(3, 3).GetString().Should().BeEmpty(); // null cell
    }
}
