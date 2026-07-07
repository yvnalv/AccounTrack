using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.MasterData.Application.Features;

// Price lists (ADR-0035): per-product Sales/Purchase prices, with a company default per type and
// optional per-customer/supplier overrides. Prices only prefill order lines — no posting impact.

public sealed record PriceListDto(
    Guid Id, string Name, PriceListType Type, bool IsDefault, bool IsActive, int ItemCount, byte[]? RowVersion);

public sealed record PriceListItemDto(Guid Id, Guid ProductId, decimal UnitPrice);

// ---- Create ----
public sealed record CreatePriceListCommand(string Name, PriceListType Type, bool IsDefault) : ICommand<Guid>;

public sealed class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreatePriceListHandler : ICommandHandler<CreatePriceListCommand, Guid>
{
    private readonly IPriceListRepository _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreatePriceListHandler(IPriceListRepository repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreatePriceListCommand request, CancellationToken ct)
    {
        if (request.IsDefault)
        {
            await ClearDefaultsAsync(_repo, request.Type, ct);
        }

        var list = PriceList.Create(request.Name, request.Type, request.IsDefault);
        _repo.Add(list);
        await _uow.SaveChangesAsync(ct);
        return list.Id;
    }

    internal static async Task ClearDefaultsAsync(IPriceListRepository repo, PriceListType type, CancellationToken ct)
    {
        foreach (var existing in await repo.ListByTypeAsync(type, ct))
        {
            existing.SetDefault(false);
        }
    }
}

// ---- Update (rename + default + active) ----
public sealed record UpdatePriceListCommand(
    Guid Id, string Name, bool IsDefault, bool IsActive, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdatePriceListValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
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

        list.Update(request.Name);
        if (request.IsActive) list.Activate(); else list.Deactivate();

        if (request.IsDefault && !list.IsDefault)
        {
            await CreatePriceListHandler.ClearDefaultsAsync(_repo, list.Type, ct);
        }

        list.SetDefault(request.IsDefault);
        await _uow.SaveChangesAsync(ct);
        return list.Id;
    }
}

// ---- Upsert / delete an item ----
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
                l.Id, l.Name, l.Type, l.IsDefault, l.IsActive, counts.GetValueOrDefault(l.Id, 0), l.RowVersion))
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
/// Resolves the applicable unit price per product for a party (ADR-0035): the company default list
/// for the type, overlaid by the party's assigned list. Returns a productId → price map the order
/// forms use to prefill line prices. An unmatched product is simply absent (line stays manual).
/// </summary>
public sealed record ResolvePricesQuery(PriceListType Type, Guid? PartyId) : IQuery<IReadOnlyDictionary<Guid, decimal>>;

public sealed class ResolvePricesHandler : IQueryHandler<ResolvePricesQuery, IReadOnlyDictionary<Guid, decimal>>
{
    private readonly IPriceListRepository _repo;
    private readonly ICodedRepository<Customer> _customers;
    private readonly ICodedRepository<Supplier> _suppliers;

    public ResolvePricesHandler(
        IPriceListRepository repo, ICodedRepository<Customer> customers, ICodedRepository<Supplier> suppliers)
    {
        _repo = repo;
        _customers = customers;
        _suppliers = suppliers;
    }

    public async Task<Result<IReadOnlyDictionary<Guid, decimal>>> Handle(ResolvePricesQuery request, CancellationToken ct)
    {
        var map = new Dictionary<Guid, decimal>();

        var defaultList = await _repo.GetDefaultAsync(request.Type, ct);
        if (defaultList is not null)
        {
            await OverlayAsync(map, defaultList.Id, ct);
        }

        var partyListId = await ResolvePartyListAsync(request.Type, request.PartyId, ct);
        if (partyListId is { } id)
        {
            var partyList = await _repo.GetAsync(id, ct);
            if (partyList is { IsActive: true } && partyList.Type == request.Type)
            {
                await OverlayAsync(map, partyList.Id, ct); // party list overrides the default
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

    private async Task OverlayAsync(Dictionary<Guid, decimal> map, Guid priceListId, CancellationToken ct)
    {
        foreach (var item in await _repo.GetItemsAsync(priceListId, ct))
        {
            map[item.ProductId] = item.UnitPrice;
        }
    }
}
