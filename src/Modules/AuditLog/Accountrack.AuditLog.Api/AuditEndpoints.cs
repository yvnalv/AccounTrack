using Accountrack.AuditLog.Application;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.AuditLog.Api;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/audit-entries", async (
                string? entityType,
                string? entityId,
                DateTime? fromUtc,
                DateTime? toUtc,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken ct) =>
            {
                var query = new GetAuditEntriesQuery(
                    entityType, entityId, fromUtc, toUtc, page ?? 1, pageSize ?? 50);
                return (await sender.Send(query, ct)).ToHttpResult();
            })
            .RequireAuthorization("Audit.View")
            .WithTags("Audit")
            .WithName("GetAuditEntries");

        return app;
    }
}
