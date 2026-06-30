using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.MasterData.Application.Features;

// Edit + activate/deactivate for master data (ADR-0029). Code is the immutable natural key, so it is
// not editable here. "Delete" is deactivation (soft, reversible) — rows are never physically removed.

// ---- Customers ----
public sealed record UpdateCustomerCommand(
    Guid Id, string Name, string? TaxId, int PaymentTermDays, decimal CreditLimit,
    byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PaymentTermDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCustomerHandler : ICommandHandler<UpdateCustomerCommand, Guid>
{
    private readonly ICodedRepository<Customer> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateCustomerHandler(ICodedRepository<Customer> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await _repo.GetByIdAsync(request.Id, ct);
        if (customer is null) return MasterDataErrors.NotFound("customer");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(customer, request.RowVersion);
        customer.Update(request.Name, request.TaxId, request.PaymentTermDays, request.CreditLimit);
        await _uow.SaveChangesAsync(ct);
        return customer.Id;
    }
}

public sealed record SetCustomerActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetCustomerActiveHandler : ICommandHandler<SetCustomerActiveCommand, Guid>
{
    private readonly ICodedRepository<Customer> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetCustomerActiveHandler(ICodedRepository<Customer> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetCustomerActiveCommand request, CancellationToken ct)
    {
        var customer = await _repo.GetByIdAsync(request.Id, ct);
        if (customer is null) return MasterDataErrors.NotFound("customer");
        if (request.IsActive) customer.Activate(); else customer.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return customer.Id;
    }
}

// ---- Suppliers ----
public sealed record UpdateSupplierCommand(
    Guid Id, string Name, string? TaxId, int PaymentTermDays, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PaymentTermDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateSupplierHandler : ICommandHandler<UpdateSupplierCommand, Guid>
{
    private readonly ICodedRepository<Supplier> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateSupplierHandler(ICodedRepository<Supplier> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _repo.GetByIdAsync(request.Id, ct);
        if (supplier is null) return MasterDataErrors.NotFound("supplier");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(supplier, request.RowVersion);
        supplier.Update(request.Name, request.TaxId, request.PaymentTermDays);
        await _uow.SaveChangesAsync(ct);
        return supplier.Id;
    }
}

public sealed record SetSupplierActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetSupplierActiveHandler : ICommandHandler<SetSupplierActiveCommand, Guid>
{
    private readonly ICodedRepository<Supplier> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetSupplierActiveHandler(ICodedRepository<Supplier> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetSupplierActiveCommand request, CancellationToken ct)
    {
        var supplier = await _repo.GetByIdAsync(request.Id, ct);
        if (supplier is null) return MasterDataErrors.NotFound("supplier");
        if (request.IsActive) supplier.Activate(); else supplier.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return supplier.Id;
    }
}

// ---- Warehouses ----
public sealed record UpdateWarehouseCommand(
    Guid Id, string Name, string? Address, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateWarehouseHandler : ICommandHandler<UpdateWarehouseCommand, Guid>
{
    private readonly ICodedRepository<Warehouse> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateWarehouseHandler(ICodedRepository<Warehouse> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateWarehouseCommand request, CancellationToken ct)
    {
        var warehouse = await _repo.GetByIdAsync(request.Id, ct);
        if (warehouse is null) return MasterDataErrors.NotFound("warehouse");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(warehouse, request.RowVersion);
        warehouse.Update(request.Name, request.Address);
        await _uow.SaveChangesAsync(ct);
        return warehouse.Id;
    }
}

public sealed record SetWarehouseActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetWarehouseActiveHandler : ICommandHandler<SetWarehouseActiveCommand, Guid>
{
    private readonly ICodedRepository<Warehouse> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetWarehouseActiveHandler(ICodedRepository<Warehouse> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetWarehouseActiveCommand request, CancellationToken ct)
    {
        var warehouse = await _repo.GetByIdAsync(request.Id, ct);
        if (warehouse is null) return MasterDataErrors.NotFound("warehouse");
        if (request.IsActive) warehouse.Activate(); else warehouse.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return warehouse.Id;
    }
}

// ---- Products ----
public sealed record UpdateProductCommand(
    Guid Id, string Name, Guid? CategoryId, bool IsStockTracked, bool IsSold, bool IsPurchased,
    byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateProductHandler : ICommandHandler<UpdateProductCommand, Guid>
{
    private readonly ICodedRepository<Product> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateProductHandler(ICodedRepository<Product> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(request.Id, ct);
        if (product is null) return MasterDataErrors.NotFound("product");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(product, request.RowVersion);
        product.Update(request.Name, request.CategoryId, request.IsStockTracked, request.IsSold, request.IsPurchased);
        await _uow.SaveChangesAsync(ct);
        return product.Id;
    }
}

public sealed record SetProductActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetProductActiveHandler : ICommandHandler<SetProductActiveCommand, Guid>
{
    private readonly ICodedRepository<Product> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetProductActiveHandler(ICodedRepository<Product> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetProductActiveCommand request, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(request.Id, ct);
        if (product is null) return MasterDataErrors.NotFound("product");
        if (request.IsActive) product.Activate(); else product.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return product.Id;
    }
}

// ---- Units of measure ----
public sealed record UpdateUomCommand(Guid Id, string Name, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateUomValidator : AbstractValidator<UpdateUomCommand>
{
    public UpdateUomValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateUomHandler : ICommandHandler<UpdateUomCommand, Guid>
{
    private readonly ICodedRepository<UnitOfMeasure> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateUomHandler(ICodedRepository<UnitOfMeasure> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateUomCommand request, CancellationToken ct)
    {
        var uom = await _repo.GetByIdAsync(request.Id, ct);
        if (uom is null) return MasterDataErrors.NotFound("unit of measure");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(uom, request.RowVersion);
        uom.Update(request.Name);
        await _uow.SaveChangesAsync(ct);
        return uom.Id;
    }
}

public sealed record SetUomActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetUomActiveHandler : ICommandHandler<SetUomActiveCommand, Guid>
{
    private readonly ICodedRepository<UnitOfMeasure> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetUomActiveHandler(ICodedRepository<UnitOfMeasure> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetUomActiveCommand request, CancellationToken ct)
    {
        var uom = await _repo.GetByIdAsync(request.Id, ct);
        if (uom is null) return MasterDataErrors.NotFound("unit of measure");
        if (request.IsActive) uom.Activate(); else uom.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return uom.Id;
    }
}

// ---- Product categories ----
public sealed record UpdateCategoryCommand(Guid Id, string Name, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateCategoryHandler : ICommandHandler<UpdateCategoryCommand, Guid>
{
    private readonly ICodedRepository<ProductCategory> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateCategoryHandler(ICodedRepository<ProductCategory> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await _repo.GetByIdAsync(request.Id, ct);
        if (category is null) return MasterDataErrors.NotFound("category");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(category, request.RowVersion);
        category.Update(request.Name);
        await _uow.SaveChangesAsync(ct);
        return category.Id;
    }
}

public sealed record SetCategoryActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetCategoryActiveHandler : ICommandHandler<SetCategoryActiveCommand, Guid>
{
    private readonly ICodedRepository<ProductCategory> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetCategoryActiveHandler(ICodedRepository<ProductCategory> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetCategoryActiveCommand request, CancellationToken ct)
    {
        var category = await _repo.GetByIdAsync(request.Id, ct);
        if (category is null) return MasterDataErrors.NotFound("category");
        if (request.IsActive) category.Activate(); else category.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return category.Id;
    }
}

// ---- Tax codes ----
public sealed record UpdateTaxCodeCommand(Guid Id, string Name, decimal Rate, byte[]? RowVersion = null) : ICommand<Guid>;

public sealed class UpdateTaxCodeValidator : AbstractValidator<UpdateTaxCodeCommand>
{
    public UpdateTaxCodeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rate).InclusiveBetween(0m, 1m);
    }
}

public sealed class UpdateTaxCodeHandler : ICommandHandler<UpdateTaxCodeCommand, Guid>
{
    private readonly ICodedRepository<TaxCode> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public UpdateTaxCodeHandler(ICodedRepository<TaxCode> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(UpdateTaxCodeCommand request, CancellationToken ct)
    {
        var taxCode = await _repo.GetByIdAsync(request.Id, ct);
        if (taxCode is null) return MasterDataErrors.NotFound("tax code");
        if (request.RowVersion is not null) _repo.SetExpectedVersion(taxCode, request.RowVersion);
        taxCode.Update(request.Name, request.Rate);
        await _uow.SaveChangesAsync(ct);
        return taxCode.Id;
    }
}

public sealed record SetTaxCodeActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetTaxCodeActiveHandler : ICommandHandler<SetTaxCodeActiveCommand, Guid>
{
    private readonly ICodedRepository<TaxCode> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public SetTaxCodeActiveHandler(ICodedRepository<TaxCode> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(SetTaxCodeActiveCommand request, CancellationToken ct)
    {
        var taxCode = await _repo.GetByIdAsync(request.Id, ct);
        if (taxCode is null) return MasterDataErrors.NotFound("tax code");
        if (request.IsActive) taxCode.Activate(); else taxCode.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return taxCode.Id;
    }
}
