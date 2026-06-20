namespace Accountrack.SharedKernel.Export;

/// <summary>
/// A header + rows of string cells — the format-neutral payload an export handler returns (ADR-0031).
/// The API layer renders it to CSV or Excel (and later PDF) based on the requested format, so
/// handlers never depend on a presentation library.
/// </summary>
public sealed record TabularData(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string?>> Rows)
{
    public static TabularData From(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows) =>
        new(header, rows.ToList());
}
