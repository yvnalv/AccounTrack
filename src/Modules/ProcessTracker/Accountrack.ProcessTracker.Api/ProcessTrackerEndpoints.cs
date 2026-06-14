using Accountrack.ProcessTracker.Application;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.ProcessTracker.Api;

public static class ProcessTrackerEndpoints
{
    public static IEndpointRouteBuilder MapProcessTrackerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/documents/{documentType}/{documentId:guid}/timeline",
                async (string documentType, Guid documentId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetTimelineQuery(documentType, documentId), ct)).ToHttpResult())
            .RequireAuthorization()
            .WithTags("Process Tracker")
            .WithName("GetDocumentTimeline");

        return app;
    }
}
