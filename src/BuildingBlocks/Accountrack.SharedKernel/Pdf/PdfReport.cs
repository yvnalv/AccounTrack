namespace Accountrack.SharedKernel.Pdf;

/// <summary>How a report row is rendered (a normal line, a section heading, a subtotal, or the
/// emphasised grand total).</summary>
public enum PdfRowStyle
{
    Normal = 0,
    SectionHeader = 1,
    Subtotal = 2,
    GrandTotal = 3,
}

/// <summary>One row of a report table. The first cell is left-aligned; the rest are right-aligned.</summary>
public sealed record PdfReportRow(IReadOnlyList<string?> Cells, PdfRowStyle Style = PdfRowStyle.Normal);

/// <summary>
/// A format-neutral financial-report model (ADR-0008/0031). Handlers assemble it from a report DTO;
/// the Web.Common renderer turns it into a styled PDF (sectioned label/value or a multi-column table).
/// </summary>
public sealed record PdfReport(
    string Title,
    string? Period,
    PdfParty Company,
    IReadOnlyList<string> Columns,
    IReadOnlyList<PdfReportRow> Rows,
    string? FooterNote)
{
    public string AccentHex { get; init; } = "#007E6E";
}
