using Accountrack.SharedKernel.Csv;
using Accountrack.Web.Common.Import;
using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Accountrack.BuildingBlocks.UnitTests;

public class ExcelReaderTests
{
    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> fill)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        fill(ws);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void ToCsv_reads_the_first_sheet_with_invariant_number_and_boolean_text()
    {
        using var xlsx = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Code";
            ws.Cell(1, 2).Value = "Rate";
            ws.Cell(1, 3).Value = "Active";
            ws.Cell(2, 1).Value = "SKU-001";
            ws.Cell(2, 2).Value = 0.11;       // number → "0.11"
            ws.Cell(2, 3).Value = true;        // boolean → "true"
            ws.Cell(3, 1).Value = "SKU-002";
            ws.Cell(3, 2).Value = 1500;        // integer → "1500"
            ws.Cell(3, 3).Value = false;
        });

        var csv = ExcelReader.ToCsv(xlsx);
        var rows = Csv.Parse(csv);

        rows.Should().HaveCount(3);
        rows[0].Should().Equal("Code", "Rate", "Active");
        rows[1].Should().Equal("SKU-001", "0.11", "true");
        rows[2].Should().Equal("SKU-002", "1500", "false");
    }

    [Fact]
    public void ToCsv_quotes_values_containing_commas_so_they_round_trip()
    {
        using var xlsx = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Code";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(2, 1).Value = "C-1";
            ws.Cell(2, 2).Value = "Acme, Inc.";
        });

        var rows = Csv.Parse(ExcelReader.ToCsv(xlsx));

        rows[1].Should().Equal("C-1", "Acme, Inc.");
    }

    [Fact]
    public void ToCsv_returns_empty_for_a_blank_sheet()
    {
        using var xlsx = BuildWorkbook(_ => { });
        ExcelReader.ToCsv(xlsx).Should().BeEmpty();
    }
}
