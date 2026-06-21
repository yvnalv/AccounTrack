using Accountrack.CompanyManagement.Application.Features;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.CompanyManagement.Api;

public static class CompanyEndpoints
{
    public sealed record CreateCompanyRequest(
        string Code, string Name, string FunctionalCurrency, int FiscalYearStartMonth, string TimeZone);

    public sealed record UpdateCompanyRequest(
        string Name, string? LegalName, string? TaxId, string TimeZone, bool IsVatRegistered);

    public sealed record SetSettingRequest(string Key, string Value);

    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var companies = app.MapGroup("/api/v1/companies").WithTags("Companies").RequireAuthorization();

        companies.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCompaniesQuery(), ct)).ToHttpResult())
            .WithName("GetCompanies");

        companies.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCompanyByIdQuery(id), ct)).ToHttpResult())
            .WithName("GetCompany");

        companies.MapPost("/", async (CreateCompanyRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateCompanyCommand(
                body.Code, body.Name, body.FunctionalCurrency, body.FiscalYearStartMonth, body.TimeZone), ct);
            return result.ToCreatedResult("/api/v1/companies");
        })
        .RequireAuthorization("Admin.Companies")
        .WithName("CreateCompany");

        companies.MapPut("/{id:guid}", async (Guid id, UpdateCompanyRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateCompanyCommand(
                id, body.Name, body.LegalName, body.TaxId, body.TimeZone, body.IsVatRegistered), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization("Admin.Companies")
        .WithName("UpdateCompany");

        companies.MapPut("/{id:guid}/settings", async (Guid id, SetSettingRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetCompanySettingCommand(id, body.Key, body.Value), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization("Admin.Companies")
        .WithName("SetCompanySetting");

        return app;
    }
}
