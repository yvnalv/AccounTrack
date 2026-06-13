using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Application.Contracts;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.MasterData.Application.Features;

// ---- Customers ----
public sealed record CreateCustomerCommand(
    string Code, string Name, string? TaxId, int PaymentTermDays, decimal CreditLimit) : ICommand<Guid>;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PaymentTermDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateCustomerHandler : ICommandHandler<CreateCustomerCommand, Guid>
{
    private readonly ICodedRepository<Customer> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreateCustomerHandler(ICodedRepository<Customer> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("customer");
        var customer = Customer.Create(code, request.Name, request.TaxId, request.PaymentTermDays, request.CreditLimit);
        _repo.Add(customer);
        await _uow.SaveChangesAsync(ct);
        return customer.Id;
    }
}

public sealed record GetCustomersQuery : IQuery<IReadOnlyList<CustomerDto>>;

public sealed class GetCustomersHandler : IQueryHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>
{
    private readonly ICodedRepository<Customer> _repo;
    public GetCustomersHandler(ICodedRepository<Customer> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<CustomerDto>>(items.Select(c => new CustomerDto(
            c.Id, c.Code, c.Name, c.TaxId, c.PaymentTermDays, c.CreditLimit, c.IsActive)).ToList());
    }
}

// ---- Suppliers ----
public sealed record CreateSupplierCommand(string Code, string Name, string? TaxId, int PaymentTermDays) : ICommand<Guid>;

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PaymentTermDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateSupplierHandler : ICommandHandler<CreateSupplierCommand, Guid>
{
    private readonly ICodedRepository<Supplier> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreateSupplierHandler(ICodedRepository<Supplier> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("supplier");
        var supplier = Supplier.Create(code, request.Name, request.TaxId, request.PaymentTermDays);
        _repo.Add(supplier);
        await _uow.SaveChangesAsync(ct);
        return supplier.Id;
    }
}

public sealed record GetSuppliersQuery : IQuery<IReadOnlyList<SupplierDto>>;

public sealed class GetSuppliersHandler : IQueryHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly ICodedRepository<Supplier> _repo;
    public GetSuppliersHandler(ICodedRepository<Supplier> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SupplierDto>>> Handle(GetSuppliersQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<SupplierDto>>(items.Select(s => new SupplierDto(
            s.Id, s.Code, s.Name, s.TaxId, s.PaymentTermDays, s.IsActive)).ToList());
    }
}

// ---- Warehouses ----
public sealed record CreateWarehouseCommand(string Code, string Name, string? Address) : ICommand<Guid>;

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateWarehouseHandler : ICommandHandler<CreateWarehouseCommand, Guid>
{
    private readonly ICodedRepository<Warehouse> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreateWarehouseHandler(ICodedRepository<Warehouse> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateWarehouseCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("warehouse");
        var warehouse = Warehouse.Create(code, request.Name, request.Address);
        _repo.Add(warehouse);
        await _uow.SaveChangesAsync(ct);
        return warehouse.Id;
    }
}

public sealed record GetWarehousesQuery : IQuery<IReadOnlyList<WarehouseDto>>;

public sealed class GetWarehousesHandler : IQueryHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>
{
    private readonly ICodedRepository<Warehouse> _repo;
    public GetWarehousesHandler(ICodedRepository<Warehouse> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<WarehouseDto>>> Handle(GetWarehousesQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<WarehouseDto>>(items.Select(w => new WarehouseDto(
            w.Id, w.Code, w.Name, w.Address, w.IsActive)).ToList());
    }
}

// ---- Tax Codes ----
public sealed record CreateTaxCodeCommand(string Code, string Name, decimal Rate) : ICommand<Guid>;

public sealed class CreateTaxCodeValidator : AbstractValidator<CreateTaxCodeCommand>
{
    public CreateTaxCodeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rate).InclusiveBetween(0m, 1m);
    }
}

public sealed class CreateTaxCodeHandler : ICommandHandler<CreateTaxCodeCommand, Guid>
{
    private readonly ICodedRepository<TaxCode> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CreateTaxCodeHandler(ICodedRepository<TaxCode> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateTaxCodeCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct)) return MasterDataErrors.CodeExists("tax code");
        var taxCode = TaxCode.Create(code, request.Name, request.Rate);
        _repo.Add(taxCode);
        await _uow.SaveChangesAsync(ct);
        return taxCode.Id;
    }
}

public sealed record GetTaxCodesQuery : IQuery<IReadOnlyList<TaxCodeDto>>;

public sealed class GetTaxCodesHandler : IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeDto>>
{
    private readonly ICodedRepository<TaxCode> _repo;
    public GetTaxCodesHandler(ICodedRepository<TaxCode> repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<TaxCodeDto>>> Handle(GetTaxCodesQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<TaxCodeDto>>(items.Select(t => new TaxCodeDto(
            t.Id, t.Code, t.Name, t.Rate, t.IsActive)).ToList());
    }
}
