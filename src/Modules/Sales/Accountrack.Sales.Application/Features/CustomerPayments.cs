using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Sales.Application.Features;

public sealed record CustomerPaymentAllocationInput(Guid ArOpenItemId, decimal Amount);

/// <summary>
/// Records a payment received from a customer and allocates it to AR open items. Atomically posts
/// Dr Cash-Bank / Cr AR control (AR account resolved by posting rules; the cash/bank GL account is
/// chosen on the payment), allocates each open item via the AR subledger, and records the payment.
/// </summary>
public sealed record PostCustomerPaymentCommand(
    Guid CustomerId, Guid CashAccountId, DateOnly PaymentDate, string? Reference, string? Notes,
    IReadOnlyList<CustomerPaymentAllocationInput> Allocations) : ICommand<Guid>, IIdempotentCommand;

public sealed class PostCustomerPaymentValidator : AbstractValidator<PostCustomerPaymentCommand>
{
    public PostCustomerPaymentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CashAccountId).NotEmpty();
        RuleFor(x => x.Allocations).NotEmpty().WithMessage("A customer payment requires at least one allocation.");
        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.ArOpenItemId).NotEmpty();
            a.RuleFor(x => x.Amount).GreaterThan(0);
        });
    }
}

public sealed class PostCustomerPaymentHandler : ICommandHandler<PostCustomerPaymentCommand, Guid>
{
    private readonly ICustomerPaymentRepository _payments;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public PostCustomerPaymentHandler(
        ICustomerPaymentRepository payments,
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

    public Task<Result<Guid>> Handle(PostCustomerPaymentCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostCustomerPaymentCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("SALES.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var sequence = await _payments.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new CustomerPaymentNumberSequence();
            _payments.AddSequence(sequence);
        }

        var number = sequence.Take(request.PaymentDate);
        var payment = CustomerPayment.Create(
            number, request.CustomerId, request.CashAccountId, company.FunctionalCurrency,
            request.PaymentDate, request.Reference, request.Notes);

        foreach (var allocation in request.Allocations)
        {
            payment.AddAllocation(allocation.ArOpenItemId, allocation.Amount);
        }

        var total = payment.TotalAmount;

        var arControl = await _accounts.ResolveAsync("CustomerPayment", PostingKeys.ArControl, PostingSelector.None, ct);
        if (arControl.IsFailure)
        {
            return arControl.Error;
        }

        // Dr Cash-Bank (chosen account) / Cr AR control (settles the receivable, carries the customer).
        var posting = new LedgerPostingRequest(
            request.PaymentDate, LedgerSource.CustomerPayment, payment.Id,
            $"Customer payment {number}",
            new[]
            {
                new LedgerLine(request.CashAccountId, total, 0m, "Cash / bank"),
                new LedgerLine(arControl.Value, 0m, total, "Accounts receivable", request.CustomerId),
            });

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        foreach (var allocation in request.Allocations)
        {
            var applied = await _subledger.AllocateAsync(
                allocation.ArOpenItemId, number, request.PaymentDate, allocation.Amount, payment.Id, ct);
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
