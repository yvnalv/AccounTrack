using System.Text;
using Accountrack.SharedKernel.Csv;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Accountrack.Web.Common.Results;

namespace Accountrack.Web.Common.Export;

/// <summary>
/// Renders a <see cref="TabularData"/> to a downloadable file in the requested format (ADR-0031):
/// <c>csv</c> (default) or <c>xlsx</c> via ClosedXML. PDF is layered on later.
/// </summary>
public static class TableExport
{
    public static byte[] Xlsx(TabularData data, string sheetName = "Export")
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(Sanitize(sheetName));

        for (var c = 0; c < data.Header.Count; c++)
        {
            ws.Cell(1, c + 1).Value = data.Header[c];
        }

        ws.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            for (var c = 0; c < row.Count; c++)
            {
                ws.Cell(r + 2, c + 1).Value = row[c] ?? string.Empty;
            }
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] Csv(TabularData data) =>
        Encoding.UTF8.GetBytes(Accountrack.SharedKernel.Csv.Csv.Write(data.Header, data.Rows));

    /// <summary>Turns a successful export result into a CSV or XLSX file response; <c>format=xlsx|csv</c>.</summary>
    public static async Task<IResult> File(Task<Result<TabularData>> task, string baseName, string? format)
    {
        var result = await task;
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        return File(result.Value, baseName, format);
    }

    /// <summary>Renders already-materialized tabular data to a CSV or XLSX file response.</summary>
    public static IResult File(TabularData data, string baseName, string? format)
    {
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        return fmt == "xlsx"
            ? Microsoft.AspNetCore.Http.Results.File(Xlsx(data, baseName), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseName}.xlsx")
            : Microsoft.AspNetCore.Http.Results.File(Csv(data), "text/csv", $"{baseName}.csv");
    }

    private static string Sanitize(string name)
    {
        var clean = new string(name.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray());
        return clean.Length == 0 ? "Export" : (clean.Length > 31 ? clean[..31] : clean);
    }
}
