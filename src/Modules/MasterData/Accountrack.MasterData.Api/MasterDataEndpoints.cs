using Accountrack.MasterData.Application.Features;
using Accountrack.Web.Common.Export;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.MasterData.Api;

public static class MasterDataEndpoints
{
    private const string View = "MasterData.View";
    private const string Create = "MasterData.Create";
    private const string Edit = "MasterData.Edit";
    private const string Delete = "MasterData.Delete";
    private const string Import = "MasterData.Import";
    private const string Export = "MasterData.Export";

    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var uoms = app.MapGroup("/api/v1/units-of-measure").WithTags("Units of Measure").RequireAuthorization();
        uoms.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetUomsQuery(), ct))).RequireAuthorization(View);
        uoms.MapPost("/", (CreateUomCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/units-of-measure")).RequireAuthorization(Create);
        uoms.MapPut("/{id:guid}", (Guid id, NameBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateUomCommand(id, b.Name), ct))).RequireAuthorization(Edit);
        uoms.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetUomActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);

        var cats = app.MapGroup("/api/v1/product-categories").WithTags("Product Categories").RequireAuthorization();
        cats.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetCategoriesQuery(), ct))).RequireAuthorization(View);
        cats.MapPost("/", (CreateCategoryCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/product-categories")).RequireAuthorization(Create);
        cats.MapPut("/{id:guid}", (Guid id, NameBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateCategoryCommand(id, b.Name), ct))).RequireAuthorization(Edit);
        cats.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetCategoryActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);

        var products = app.MapGroup("/api/v1/products").WithTags("Products").RequireAuthorization();
        products.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetProductsQuery(), ct))).RequireAuthorization(View);
        products.MapPost("/", (CreateProductCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/products")).RequireAuthorization(Create);
        products.MapPut("/{id:guid}", (Guid id, UpdateProductBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateProductCommand(id, b.Name, b.CategoryId, b.IsStockTracked, b.IsSold, b.IsPurchased), ct))).RequireAuthorization(Edit);
        products.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetProductActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);
        products.MapGet("/import/template", () =>
                Results.File(System.Text.Encoding.UTF8.GetBytes(ProductImportColumns.Template()), "text/csv", "products-template.csv"))
            .RequireAuthorization(Import).WithName("ProductImportTemplate");
        products.MapPost("/import/preview", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new PreviewProductImportQuery(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("ProductImportPreview");
        products.MapPost("/import/commit", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new CommitProductImportCommand(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("ProductImportCommit");
        products.MapGet("/export", async (string? format, ISender s, CancellationToken ct) =>
                await TableExport.File(s.Send(new ExportProductsQuery(), ct), "products", format))
            .RequireAuthorization(Export).WithName("ProductExport");

        var customers = app.MapGroup("/api/v1/customers").WithTags("Customers").RequireAuthorization();
        customers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetCustomersQuery(), ct))).RequireAuthorization(View);
        customers.MapPost("/", (CreateCustomerCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/customers")).RequireAuthorization(Create);
        customers.MapPut("/{id:guid}", (Guid id, UpdateCustomerBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateCustomerCommand(id, b.Name, b.TaxId, b.PaymentTermDays, b.CreditLimit), ct))).RequireAuthorization(Edit);
        customers.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetCustomerActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);

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

        customers.MapGet("/export", async (string? format, ISender s, CancellationToken ct) =>
                await TableExport.File(s.Send(new ExportCustomersQuery(), ct), "customers", format))
            .RequireAuthorization(Export).WithName("CustomerExport");

        var suppliers = app.MapGroup("/api/v1/suppliers").WithTags("Suppliers").RequireAuthorization();
        suppliers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetSuppliersQuery(), ct))).RequireAuthorization(View);
        suppliers.MapPost("/", (CreateSupplierCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/suppliers")).RequireAuthorization(Create);
        suppliers.MapPut("/{id:guid}", (Guid id, UpdateSupplierBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateSupplierCommand(id, b.Name, b.TaxId, b.PaymentTermDays), ct))).RequireAuthorization(Edit);
        suppliers.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetSupplierActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);
        suppliers.MapGet("/import/template", () =>
                Results.File(System.Text.Encoding.UTF8.GetBytes(SupplierImportColumns.Template()), "text/csv", "suppliers-template.csv"))
            .RequireAuthorization(Import).WithName("SupplierImportTemplate");
        suppliers.MapPost("/import/preview", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new PreviewSupplierImportQuery(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("SupplierImportPreview");
        suppliers.MapPost("/import/commit", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new CommitSupplierImportCommand(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("SupplierImportCommit");
        suppliers.MapGet("/export", async (string? format, ISender s, CancellationToken ct) =>
                await TableExport.File(s.Send(new ExportSuppliersQuery(), ct), "suppliers", format))
            .RequireAuthorization(Export).WithName("SupplierExport");

        var warehouses = app.MapGroup("/api/v1/warehouses").WithTags("Warehouses").RequireAuthorization();
        warehouses.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetWarehousesQuery(), ct))).RequireAuthorization(View);
        warehouses.MapPost("/", (CreateWarehouseCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/warehouses")).RequireAuthorization(Create);
        warehouses.MapPut("/{id:guid}", (Guid id, UpdateWarehouseBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateWarehouseCommand(id, b.Name, b.Address), ct))).RequireAuthorization(Edit);
        warehouses.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetWarehouseActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);
        warehouses.MapGet("/import/template", () =>
                Results.File(System.Text.Encoding.UTF8.GetBytes(WarehouseImportColumns.Template()), "text/csv", "warehouses-template.csv"))
            .RequireAuthorization(Import).WithName("WarehouseImportTemplate");
        warehouses.MapPost("/import/preview", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new PreviewWarehouseImportQuery(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("WarehouseImportPreview");
        warehouses.MapPost("/import/commit", async (IFormFile file, ISender s, CancellationToken ct) =>
                await Send(s.Send(new CommitWarehouseImportCommand(await ReadAsync(file, ct)), ct)))
            .RequireAuthorization(Import).DisableAntiforgery().WithName("WarehouseImportCommit");
        warehouses.MapGet("/export", async (string? format, ISender s, CancellationToken ct) =>
                await TableExport.File(s.Send(new ExportWarehousesQuery(), ct), "warehouses", format))
            .RequireAuthorization(Export).WithName("WarehouseExport");

        var taxCodes = app.MapGroup("/api/v1/tax-codes").WithTags("Tax Codes").RequireAuthorization();
        taxCodes.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetTaxCodesQuery(), ct))).RequireAuthorization(View);
        taxCodes.MapPost("/", (CreateTaxCodeCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/tax-codes")).RequireAuthorization(Create);
        taxCodes.MapPut("/{id:guid}", (Guid id, UpdateTaxCodeBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new UpdateTaxCodeCommand(id, b.Name, b.Rate), ct))).RequireAuthorization(Edit);
        taxCodes.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
            Send(s.Send(new SetTaxCodeActiveCommand(id, b.IsActive), ct))).RequireAuthorization(Delete);

        return app;
    }

    // Request bodies for edits — the id comes from the route, the rest from the body.
    public sealed record SetActiveBody(bool IsActive);
    public sealed record NameBody(string Name);
    public sealed record UpdateTaxCodeBody(string Name, decimal Rate);
    public sealed record UpdateCustomerBody(string Name, string? TaxId, int PaymentTermDays, decimal CreditLimit);
    public sealed record UpdateSupplierBody(string Name, string? TaxId, int PaymentTermDays);
    public sealed record UpdateWarehouseBody(string Name, string? Address);
    public sealed record UpdateProductBody(string Name, Guid? CategoryId, bool IsStockTracked, bool IsSold, bool IsPurchased);

    /// <summary>Reads an uploaded import file as CSV text, converting an .xlsx upload on the fly so
    /// the CSV import pipeline ingests both formats unchanged (ADR-0031).</summary>
    private static async Task<string> ReadAsync(IFormFile file, CancellationToken ct)
    {
        var isXlsx = file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            || (file.ContentType?.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase) ?? false);

        if (isXlsx)
        {
            await using var xlsx = file.OpenReadStream();
            return Accountrack.Web.Common.Import.ExcelReader.ToCsv(xlsx);
        }

        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync(ct);
    }

    private static async Task<IResult> Send<T>(Task<SharedKernel.Results.Result<T>> task) =>
        (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<SharedKernel.Results.Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
