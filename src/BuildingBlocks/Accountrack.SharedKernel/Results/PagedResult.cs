namespace Accountrack.SharedKernel.Results;

/// <summary>A page of results plus paging metadata (API_SPEC.md §2 collection envelope).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalItems)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
