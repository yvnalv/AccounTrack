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

        var customers = app.MapGroup("/api/v1/customers").WithTags("Customers").RequireAuthorization();
        customers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetCustomersQuery(), ct))).RequireAuthorization(View);
        customers.MapPost("/", (CreateCustomerCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/customers")).RequireAuthorization(Manage);

        var suppliers = app.MapGroup("/api/v1/suppliers").WithTags("Suppliers").RequireAuthorization();
        suppliers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetSuppliersQuery(), ct))).RequireAuthorization(View);
        suppliers.MapPost("/", (CreateSupplierCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/suppliers")).RequireAuthorization(Manage);

        var warehouses = app.MapGroup("/api/v1/warehouses").WithTags("Warehouses").RequireAuthorization();
        warehouses.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetWarehousesQuery(), ct))).RequireAuthorization(View);
        warehouses.MapPost("/", (CreateWarehouseCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/warehouses")).RequireAuthorization(Manage);

        var taxCodes = app.MapGroup("/api/v1/tax-codes").WithTags("Tax Codes").RequireAuthorization();
        taxCodes.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetTaxCodesQuery(), ct))).RequireAuthorization(View);
        taxCodes.MapPost("/", (CreateTaxCodeCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct), "/api/v1/tax-codes")).RequireAuthorization(Manage);

        return app;
    }

    private static async Task<IResult> Send<T>(Task<SharedKernel.Results.Result<T>> task) =>
        (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<SharedKernel.Results.Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
