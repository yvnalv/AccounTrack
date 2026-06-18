using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Purchasing.Application.Features;

public sealed record SupplierPaymentAllocationInput(Guid ApOpenItemId, decimal Amount);

/// <summary>
/// Pays a supplier and allocates the amount to AP open items. Atomically posts
/// Dr AP control / Cr Cash-Bank (AP account resolved by posting rules; the cash/bank GL account is
/// chosen on the payment), allocates each open item via the AP subledger, and records the payment.
/// </summary>
public sealed record PostSupplierPaymentCommand(
    Guid SupplierId, Guid CashAccountId, DateOnly PaymentDate, string? Reference, string? Notes,
    IReadOnlyList<SupplierPaymentAllocationInput> Allocations) : ICommand<Guid>, IIdempotentCommand;

public sealed class PostSupplierPaymentValidator : AbstractValidator<PostSupplierPaymentCommand>
{
    public PostSupplierPaymentValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.CashAccountId).NotEmpty();
        RuleFor(x => x.Allocations).NotEmpty().WithMessage("A supplier payment requires at least one allocation.");
        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.ApOpenItemId).NotEmpty();
            a.RuleFor(x => x.Amount).GreaterThan(0);
        });
    }
}

public sealed class PostSupplierPaymentHandler : ICommandHandler<PostSupplierPaymentCommand, Guid>
{
    private readonly ISupplierPaymentRepository _payments;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public PostSupplierPaymentHandler(
        ISupplierPaymentRepository payments,
        ICrossModuleUnitOfWork uow,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts,
        ISubledgerPosting subledger,
        ICompanyDirectory companies,
        ITenantContext tenant)
    {
        _payments = payments;
        _uow = uow;
        _ledger = ledger;
        _accounts = accounts;
        _subledger = subledger;
        _companies = companies;
        _tenant = tenant;
    }

    public Task<Result<Guid>> Handle(PostSupplierPaymentCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostSupplierPaymentCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("PURCHASING.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var sequence = await _payments.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new SupplierPaymentNumberSequence();
            _payments.AddSequence(sequence);
        }

        var number = sequence.Take(request.PaymentDate);
        var payment = SupplierPayment.Create(
            number, request.SupplierId, request.CashAccountId, company.FunctionalCurrency,
            request.PaymentDate, request.Reference, request.Notes);

        foreach (var allocation in request.Allocations)
        {
            payment.AddAllocation(allocation.ApOpenItemId, allocation.Amount);
        }

        var total = payment.TotalAmount;

        var apControl = await _accounts.ResolveAsync("SupplierPayment", PostingKeys.ApControl, PostingSelector.None, ct);
        if (apControl.IsFailure)
        {
            return apControl.Error;
        }

        // Dr AP control (settles the payable, carries the supplier) / Cr Cash-Bank (chosen account).
        var posting = new LedgerPostingRequest(
            request.PaymentDate, LedgerSource.SupplierPayment, payment.Id,
            $"Supplier payment {number}",
            new[]
            {
                new LedgerLine(apControl.Value, total, 0m, "Accounts payable", request.SupplierId),
                new LedgerLine(request.CashAccountId, 0m, total, "Cash / bank"),
            });

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        foreach (var allocation in request.Allocations)
        {
            var applied = await _subledger.AllocateAsync(
                allocation.ApOpenItemId, number, request.PaymentDate, allocation.Amount, payment.Id, ct);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        payment.SetJournal(journal.Value);
        _payments.Add(payment);

        return payment.Id;
    }
}
