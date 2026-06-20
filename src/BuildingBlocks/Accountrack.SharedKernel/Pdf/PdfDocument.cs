namespace Accountrack.SharedKernel.Pdf;

/// <summary>A labelled key/value shown in the document meta block (e.g. "Date" / "2026-06-20").</summary>
public sealed record PdfField(string Label, string Value);

/// <summary>A named party with optional extra lines (tax id, address) for the From / Bill-To boxes.</summary>
public sealed record PdfParty(string Name, IReadOnlyList<string> Lines);

/// <summary>One row of the document's line-item table (all pre-formatted strings).</summary>
public sealed record PdfLineItem(string Description, string Quantity, string UnitPrice, string Tax, string Amount);

/// <summary>A totals row; <see cref="Emphasis"/> renders it as the grand total.</summary>
public sealed record PdfTotalLine(string Label, string Value, bool Emphasis = false);

/// <summary>
/// A format-neutral business-document model (ADR-0031). Application handlers assemble it; the
/// Web.Common renderer turns it into a styled PDF, so handlers never depend on a PDF library.
/// </summary>
public sealed record PdfDocument(
    string Title,
    string Number,
    PdfParty Seller,
    string BillToLabel,
    PdfParty BillTo,
    IReadOnlyList<PdfField> Meta,
    IReadOnlyList<PdfLineItem> Lines,
    IReadOnlyList<PdfTotalLine> Totals,
    string? Notes,
    string? FooterNote)
{
    /// <summary>Brand accent (teal) used for the title bar, table header, and grand total.</summary>
    public string AccentHex { get; init; } = "#007E6E";
}
