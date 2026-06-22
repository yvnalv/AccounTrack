using System.Globalization;
using ClosedXML.Excel;
using Csv = Accountrack.SharedKernel.Csv.Csv;

namespace Accountrack.Web.Common.Import;

/// <summary>
/// Reads the first worksheet of an uploaded <c>.xlsx</c> file into a CSV string, so the existing CSV
/// import pipeline (parse → dry-run preview → commit) can ingest Excel files unchanged (ADR-0031).
/// Numbers, booleans and dates are emitted in an invariant, parser-friendly form.
/// </summary>
public static class ExcelReader
{
    public static string ToCsv(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        var range = worksheet?.RangeUsed();
        if (worksheet is null || range is null)
        {
            return string.Empty;
        }

        var columnCount = range.ColumnCount();
        var rows = new List<IReadOnlyList<string?>>();
        foreach (var row in range.Rows())
        {
            var cells = new string?[columnCount];
            for (var c = 1; c <= columnCount; c++)
            {
                cells[c - 1] = CellText(row.Cell(c));
            }

            rows.Add(cells);
        }

        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var header = rows[0].Select(h => h ?? string.Empty).ToList();
        return Csv.Write(header, rows.Skip(1));
    }

    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.Number => cell.Value.GetNumber().ToString(CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.Value.GetBoolean() ? "true" : "false",
        XLDataType.DateTime => cell.Value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Blank => string.Empty,
        _ => cell.GetString(),
    };
}
