using Accountrack.MasterData.Application.Features;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.MasterData.Api;

public static class MasterDataEndpoints
{
    private const string View = "MasterData.View";
    private const string Manage = "MasterData.Manage";
    private const string Import = "MasterData.Import";
    private const string Export = "MasterData.Export";

    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var uoms = app.MapGroup("/api/v1/units-of-measure").WithTags("Units of Measure").RequireAuthorization();
        uoms.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetUomsQuery(), ct))).RequireAuthorization(View);
        uoms.MapPost("/", (CreateUomCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/units-of-measure")).RequireAuthorization(Manage);

        var cats = app.MapGroup("/api/v1/product-categories").WithTags("Product Categories").RequireAuthorization();
        cats.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetCategoriesQuery(), ct))).RequireAuthorization(View);
        cats.MapPost("/", (CreateCategoryCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/product-categories")).RequireAuthorization(Manage);

        var products = app.MapGroup("/api/v1/products").WithTags("Products").RequireAuthorization();
        products.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetProductsQuery(), ct))).RequireAuthorization(View);
        products.MapPost("/", (CreateProductCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/products")).RequireAuthorization(Manage);
        products.MapPut("/{id:guid}", (Guid id, UpdateProductBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateProductCommand(id, b.Name, b.CategoryId, b.IsStockTracked, b.IsSold, b.IsPurchased), ct))).RequireAuthorization(Manage);
        products.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetProductActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Manage);

        var customers = app.MapGroup("/api/v1/customers").WithTags("Customers").RequireAuthorization();
        customers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetCustomersQuery(), ct))).RequireAuthorization(View);
        customers.MapPost("/", (CreateCustomerCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/customers")).RequireAuthorization(Manage);
        customers.MapPut("/{id:guid}", (Guid id, UpdateCustomerBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateCustomerCommand(id, b.Name, b.TaxId, b.PaymentTermDays, b.CreditLimit), ct))).RequireAuthorization(Manage);
        customers.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetCustomerActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Manage);

        // --- Import / export (ADR-0031) ---
        customers.MapGet("/import/template", () =>
                Results.File(System.Text.Encoding.UTF8.GetBytes(CustomerImportColumns.Template()), "text/csv", "customers-template.csv"))
            .RequireAuthorization(Import).WithName("CustomerImportTemplate");

        customers.MapPost("/import/preview", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new PreviewCustomerImportQuery(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("CustomerImportPreview");

        customers.MapPost("/import/commit", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new CommitCustomerImportCommand(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("CustomerImportCommit");

        customers.MapGet("/export", async (ISender s, CancellationToken ct) =>
                await Csv(s.Send(new ExportCustomersQuery(), ct), "customers.csv"))
            .RequireAuthorization(Export).WithName("CustomerExport");

        var suppliers = app.MapGroup("/api/v1/suppliers").WithTags("Suppliers").RequireAuthorization();
        suppliers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetSuppliersQuery(), ct))).RequireAuthorization(View);
        suppliers.MapPost("/", (CreateSupplierCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/suppliers")).RequireAuthorization(Manage);
        suppliers.MapPut("/{id:guid}", (Guid id, UpdateSupplierBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateSupplierCommand(id, b.Name, b.TaxId, b.PaymentTermDays), ct))).RequireAuthorization(Manage);
        suppliers.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetSupplierActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Manage);

        var warehouses = app.MapGroup("/api/v1/warehouses").WithTags("Warehouses").RequireAuthorization();
        warehouses.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetWarehousesQuery(), ct))).RequireAuthorization(View);
        warehouses.MapPost("/", (CreateWarehouseCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/warehouses")).RequireAuthorization(Manage);
        warehouses.MapPut("/{id:guid}", (Guid id, UpdateWarehouseBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateWarehouseCommand(id, b.Name, b.Address), ct))).RequireAuthorization(Manage);
        warehouses.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetWarehouseActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Manage);

        var taxCodes = app.MapGroup("/api/v1/tax-codes").WithTags("Tax Codes").RequireAuthorization();
        taxCodes.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetTaxCodesQuery(), ct))).RequireAuthorization(View);
        taxCodes.MapPost("/", (CreateTaxCodeCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/tax-codes")).RequireAuthorization(Manage);

        return app;
    }

    // Request bodies for edits — the id comes from the route, the rest from the body.
    public sealed record SetActiveBody(bool IsActive);
    public sealed record UpdateCustomerBody(string Name, string? TaxId, int PaymentTermDays, decimal CreditLimit);
    public sealed record UpdateSupplierBody(string Name, string? TaxId, int PaymentTermDays);
    public sealed record UpdateWarehouseBody(string Name, string? Address);
    public sealed record UpdateProductBody(string Name, Guid? CategoryId, bool IsStockTracked, bool IsSold, bool IsPurchased);

    private static async Task<string> ReadAsync(IFormFile file, CancellationToken ct)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync(ct);
    }

    /// <summary>Returns a successful CSV <see cref="SharedKernel.Results.Result{T}"/> as a file download.</summary>
    private static async Task<IResult> Csv(Task<SharedKernel.Results.Result<string>> task, string fileName)
    {
        var result = await task;
        return result.IsSuccess
            ? Results.File(System.Text.Encoding.UTF8.GetBytes(result.Value), "text/csv", fileName)
            : result.ToHttpResult();
    }

    private static async Task<IResult> Send<T>(Task<SharedKernel.Results.Result<T>> task) =>
        (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<SharedKernel.Results.Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
