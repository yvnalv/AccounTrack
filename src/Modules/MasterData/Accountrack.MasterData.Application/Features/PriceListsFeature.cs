using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.MasterData.Application.Features;

// Price lists (ADR-0035): the product base price is the default (Product.SalePrice/PurchasePrice); a
// price list is a shared rule — a % discount off base plus optional per-product fixed overrides — that
// customers/suppliers can point at. Prices only prefill order lines (no posting impact).

public sealed record PriceListDto(
    Guid Id, string Name, PriceListType Type, decimal DiscountPercent, bool IsActive, int ItemCount, byte[]? RowVersion);

public sealed record PriceListItemDto(Guid Id, Guid ProductId, decimal UnitPrice);

// ---- Create ----
public sealed record CreatePriceListCommand(string Name, PriceListType Type, decimal DiscountPercent) : ICommand<Guid>;

public sealed class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0m, 100m);
    }
}

public sealed class CreatePriceListHandler : ICommandHandler<CreatePriceListCommand, Guid>
{
    private readonly IPriceListRepository _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreatePriceListHandler(IPriceListRepository repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreatePriceListCommand request, CancellationToken ct)
    {
        var list = PriceList.Create(request.Name, request.Type, request.DiscountPercent);
        _repo.Add(list);
        await _uow.SaveChangesAsync(ct);
        return list.Id;
    }
}

// ---- Update (rename + discount + active) ----
public sealed record UpdatePriceListCommand(
    Guid Id, string Name, decimal DiscountPercent, bool IsActive, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdatePriceListValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0m, 100m);
    }
}

public sealed class UpdatePriceListHandler : ICommandHandler<UpdatePriceListCommand, Guid>
{
    private readonly IPriceListRepository _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdatePriceListHandler(IPriceListRepository repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdatePriceListCommand request, CancellationToken ct)
    {
        var list = await _repo.GetAsync(request.Id, ct);
        if (list is null) return MasterDataErrors.NotFound("price list");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(list, request.RowVersion);

        list.Update(request.Name, request.DiscountPercent);
        if (request.IsActive) list.Activate(); else list.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return list.Id;
    }
}

// ---- Upsert / delete an override item ----
public sealed record UpsertPriceListItemCommand(Guid PriceListId, Guid ProductId, decimal UnitPrice) : ICommand<Guid>;

public sealed class UpsertPriceListItemValidator : AbstractValidator<UpsertPriceListItemCommand>
{
    public UpsertPriceListItemValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0m);
    }
}

public sealed class UpsertPriceListItemHandler : ICommandHandler<UpsertPriceListItemCommand, Guid>
{
    private readonly IPriceListRepository _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpsertPriceListItemHandler(IPriceListRepository repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpsertPriceListItemCommand request, CancellationToken ct)
    {
        var list = await _repo.GetAsync(request.PriceListId, ct);
        if (list is null) return MasterDataErrors.NotFound("price list");

        var item = await _repo.GetItemAsync(request.PriceListId, request.ProductId, ct);
        if (item is null)
        {
            item = PriceListItem.Create(request.PriceListId, request.ProductId, request.UnitPrice);
            _repo.AddItem(item);
        }
        else
        {
            item.SetPrice(request.UnitPrice);
        }

        await _uow.SaveChangesAsync(ct);
        return item.Id;
    }
}

public sealed record DeletePriceListItemCommand(Guid PriceListId, Guid ProductId) : ICommand<Guid>;

public sealed class DeletePriceListItemHandler : ICommandHandler<DeletePriceListItemCommand, Guid>
{
    private readonly IPriceListRepository _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public DeletePriceListItemHandler(IPriceListRepository repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(DeletePriceListItemCommand request, CancellationToken ct)
    {
        var item = await _repo.GetItemAsync(request.PriceListId, request.ProductId, ct);
        if (item is null) return MasterDataErrors.NotFound("price list item");
        _repo.RemoveItem(item);
        await _uow.SaveChangesAsync(ct);
        return item.Id;
    }
}

// ---- Queries ----
public sealed record GetPriceListsQuery : IQuery<IReadOnlyList<PriceListDto>>;

public sealed class GetPriceListsHandler : IQueryHandler<GetPriceListsQuery, IReadOnlyList<PriceListDto>>
{
    private readonly IPriceListRepository _repo;
    public GetPriceListsHandler(IPriceListRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<PriceListDto>>> Handle(GetPriceListsQuery request, CancellationToken ct)
    {
        var lists = await _repo.ListAsync(ct);
        var counts = await _repo.ItemCountsAsync(ct);
        return Result.Success<IReadOnlyList<PriceListDto>>(lists
            .Select(l => new PriceListDto(
                l.Id, l.Name, l.Type, l.DiscountPercent, l.IsActive, counts.GetValueOrDefault(l.Id, 0), l.RowVersion))
            .ToList());
    }
}

public sealed record GetPriceListItemsQuery(Guid PriceListId) : IQuery<IReadOnlyList<PriceListItemDto>>;

public sealed class GetPriceListItemsHandler : IQueryHandler<GetPriceListItemsQuery, IReadOnlyList<PriceListItemDto>>
{
    private readonly IPriceListRepository _repo;
    public GetPriceListItemsHandler(IPriceListRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<PriceListItemDto>>> Handle(GetPriceListItemsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetItemsAsync(request.PriceListId, ct);
        return Result.Success<IReadOnlyList<PriceListItemDto>>(
            items.Select(i => new PriceListItemDto(i.Id, i.ProductId, i.UnitPrice)).ToList());
    }
}

/// <summary>
/// Resolves the party-specific unit price per product (ADR-0035). The product base price
/// (SalePrice/PurchasePrice) is the default and lives on the product; this returns only the
/// <em>adjustments</em> a party's assigned list makes: a per-product override, or the list's %
/// discount off base. Products without an adjustment are absent (the order form uses the base price).
/// An unassigned party (or an inactive list) yields an empty map.
/// </summary>
public sealed record ResolvePricesQuery(PriceListType Type, Guid? PartyId) : IQuery<IReadOnlyDictionary<Guid, decimal>>;

public sealed class ResolvePricesHandler : IQueryHandler<ResolvePricesQuery, IReadOnlyDictionary<Guid, decimal>>
{
    private const int PriceScale = 4;

    private readonly IPriceListRepository _repo;
    private readonly ICodedRepository<Customer> _customers;
    private readonly ICodedRepository<Supplier> _suppliers;
    private readonly ICodedRepository<Product> _products;

    public ResolvePricesHandler(
        IPriceListRepository repo, ICodedRepository<Customer> customers,
        ICodedRepository<Supplier> suppliers, ICodedRepository<Product> products)
    {
        _repo = repo;
        _customers = customers;
        _suppliers = suppliers;
        _products = products;
    }

    public async Task<Result<IReadOnlyDictionary<Guid, decimal>>> Handle(ResolvePricesQuery request, CancellationToken ct)
    {
        var empty = (IReadOnlyDictionary<Guid, decimal>)new Dictionary<Guid, decimal>();

        var listId = await ResolvePartyListAsync(request.Type, request.PartyId, ct);
        if (listId is not { } id) return Result.Success(empty);

        var list = await _repo.GetAsync(id, ct);
        if (list is not { IsActive: true } || list.Type != request.Type) return Result.Success(empty);

        var overrides = (await _repo.GetItemsAsync(list.Id, ct)).ToDictionary(i => i.ProductId, i => i.UnitPrice);
        var products = await _products.ListAsync(ct);

        var map = new Dictionary<Guid, decimal>();
        foreach (var product in products)
        {
            if (overrides.TryGetValue(product.Id, out var overridePrice))
            {
                map[product.Id] = overridePrice; // a fixed override wins over the discount
                continue;
            }

            if (list.DiscountPercent <= 0m) continue; // no adjustment → the base price (used by the form) stands

            var basePrice = request.Type == PriceListType.Sales ? product.SalePrice : product.PurchasePrice;
            if (basePrice is { } bp)
            {
                map[product.Id] = Math.Round(bp * (1m - (list.DiscountPercent / 100m)), PriceScale, MidpointRounding.ToEven);
            }
        }

        return Result.Success<IReadOnlyDictionary<Guid, decimal>>(map);
    }

    private async Task<Guid?> ResolvePartyListAsync(PriceListType type, Guid? partyId, CancellationToken ct)
    {
        if (partyId is not { } pid) return null;
        return type == PriceListType.Sales
            ? (await _customers.GetByIdAsync(pid, ct))?.SalesPriceListId
            : (await _suppliers.GetByIdAsync(pid, ct))?.PurchasePriceListId;
    }
}
