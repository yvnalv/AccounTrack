namespace Accountrack.Application.Abstractions.Import;

/// <summary>What an import row will do (or did), determined by matching its natural key.</summary>
public enum ImportRowAction
{
    Create = 0,
    Update = 1,
    Error = 2,
}

/// <summary>The outcome for a single import row (ADR-0031). 1-based <see cref="RowNumber"/> excludes the header.</summary>
public sealed record ImportRowResult(
    int RowNumber, ImportRowAction Action, string? Key, string? Name, IReadOnlyList<string> Errors);

/// <summary>Dry-run preview of an import: per-row results plus counts. Nothing is written.</summary>
public sealed record ImportPreviewResult(
    int TotalRows, int ToCreate, int ToUpdate, int ErrorRows, IReadOnlyList<ImportRowResult> Rows);

/// <summary>The result of committing an import (all-or-nothing): counts plus the per-row results.</summary>
public sealed record ImportCommitResult(
    bool Committed, int Created, int Updated, int ErrorRows, IReadOnlyList<ImportRowResult> Rows);
