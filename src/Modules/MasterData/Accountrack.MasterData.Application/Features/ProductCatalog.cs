using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Application.Contracts;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Inventory;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.MasterData.Application.Features;

// ---- Units of Measure ----
public sealed record CreateUomCommand(string Code, string Name) : ICommand<Guid>;

public sealed class CreateUomValidator : AbstractValidator<CreateUomCommand>
{
    public CreateUomValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateUomHandler : ICommandHandler<CreateUomCommand, Guid>
{
    private readonly ICodedRepository<UnitOfMeasure> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreateUomHandler(ICodedRepository<UnitOfMeasure> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateUomCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("unit of measure");
        var uom = UnitOfMeasure.Create(code, request.Name);
        _repo.Add(uom);
        await _uow.SaveChangesAsync(ct);
        return uom.Id;
    }
}

public sealed record GetUomsQuery : IQuery<IReadOnlyList<UnitOfMeasureDto>>;

public sealed class GetUomsHandler : IQueryHandler<GetUomsQuery, IReadOnlyList<UnitOfMeasureDto>>
{
    private readonly ICodedRepository<UnitOfMeasure> _repo;
    public GetUomsHandler(ICodedRepository<UnitOfMeasure> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<UnitOfMeasureDto>>> Handle(GetUomsQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<UnitOfMeasureDto>>(
            items.Select(u => new UnitOfMeasureDto(u.Id, u.Code, u.Name, u.IsActive, u.RowVersion)).ToList());
    }
}

// ---- Product Categories ----
public sealed record CreateCategoryCommand(string Code, string Name) : ICommand<Guid>;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateCategoryHandler : ICommandHandler<CreateCategoryCommand, Guid>
{
    private readonly ICodedRepository<ProductCategory> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreateCategoryHandler(ICodedRepository<ProductCategory> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("category");
        var category = ProductCategory.Create(code, request.Name);
        _repo.Add(category);
        await _uow.SaveChangesAsync(ct);
        return category.Id;
    }
}

public sealed record GetCategoriesQuery : IQuery<IReadOnlyList<ProductCategoryDto>>;

public sealed class GetCategoriesHandler : IQueryHandler<GetCategoriesQuery, IReadOnlyList<ProductCategoryDto>>
{
    private readonly ICodedRepository<ProductCategory> _repo;
    public GetCategoriesHandler(ICodedRepository<ProductCategory> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<ProductCategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<ProductCategoryDto>>(
            items.Select(c => new ProductCategoryDto(c.Id, c.Code, c.Name, c.IsActive, c.RowVersion)).ToList());
    }
}

// ---- Products ----
public sealed record CreateProductCommand(
    string Code, string Name, Guid BaseUomId, Guid? CategoryId,
    bool IsStockTracked, bool IsSold, bool IsPurchased,
    CostingMethod CostingMethod = CostingMethod.MovingAverage,
    decimal? SalePrice = null, decimal? PurchasePrice = null) : ICommand<Guid>;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUomId).NotEmpty();
    }
}

public sealed class CreateProductHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly ICodedRepository<Product> _products;
    private readonly ICodedRepository<UnitOfMeasure> _uoms;
    private readonly IMasterDataUnitOfWork _uow;

    public CreateProductHandler(
        ICodedRepository<Product> products, ICodedRepository<UnitOfMeasure> uoms, IMasterDataUnitOfWork uow)
    {
        _products = products;
        _uoms = uoms;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _products.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("product");
        if (!await _uoms.ExistsAsync(request.BaseUomId, ct)) return MasterDataErrors.UomNotFound;

        var product = Product.Create(
            code, request.Name, request.BaseUomId, request.CategoryId,
            request.IsStockTracked, request.IsSold, request.IsPurchased, request.CostingMethod,
            request.SalePrice, request.PurchasePrice);
        _products.Add(product);
        await _uow.SaveChangesAsync(ct);
        return product.Id;
    }
}

public sealed record GetProductsQuery : IQuery<IReadOnlyList<ProductDto>>;

public sealed class GetProductsHandler : IQueryHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly ICodedRepository<Product> _repo;
    public GetProductsHandler(ICodedRepository<Product> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<ProductDto>>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<ProductDto>>(items.Select(p => new ProductDto(
            p.Id, p.Code, p.Name, p.BaseUomId, p.CategoryId, p.IsStockTracked, p.IsSold, p.IsPurchased, p.IsActive,
            p.RowVersion, p.CostingMethod, p.SalePrice, p.PurchasePrice)).ToList());
    }
}
