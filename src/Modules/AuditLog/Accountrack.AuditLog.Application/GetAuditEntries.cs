using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;

namespace Accountrack.AuditLog.Application;

public sealed record AuditEntryDto(
    Guid Id,
    Guid TenantId,
    Guid? CompanyId,
    string EntityType,
    string EntityId,
    string Action,
    string ChangesJson,
    Guid UserId,
    DateTime TimestampUtc);

public sealed record AuditEntryFilter(
    string? EntityType,
    string? EntityId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

/// <summary>Read port over the append-only audit store (implemented in Infrastructure).</summary>
public interface IAuditReadStore
{
    Task<PagedResult<AuditEntryDto>> QueryAsync(AuditEntryFilter filter, CancellationToken ct);
}

/// <summary>Lists audit entries for the current tenant, filtered and paged (newest first).</summary>
public sealed record GetAuditEntriesQuery(
    string? EntityType,
    string? EntityId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<AuditEntryDto>>;

public sealed class GetAuditEntriesQueryHandler
    : IQueryHandler<GetAuditEntriesQuery, PagedResult<AuditEntryDto>>
{
    private const int MaxPageSize = 200;

    private readonly IAuditReadStore _store;

    public GetAuditEntriesQueryHandler(IAuditReadStore store) => _store = store;

    public async Task<Result<PagedResult<AuditEntryDto>>> Handle(
        GetAuditEntriesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var filter = new AuditEntryFilter(
            request.EntityType, request.EntityId, request.FromUtc, request.ToUtc, page, pageSize);

        return Result.Success(await _store.QueryAsync(filter, cancellationToken));
    }
}
