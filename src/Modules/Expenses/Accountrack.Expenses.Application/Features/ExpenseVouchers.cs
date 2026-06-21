using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Contracts;
using Accountrack.Expenses.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Expenses.Application.Features;

public sealed record ExpenseVoucherLineInput(Guid ExpenseCategoryId, string? Description, decimal Amount, decimal TaxRate);

/// <summary>
/// Records and posts an operating-expense voucher (ADR-0030). Atomically posts Dr Expense per category
/// (accounts resolved by the posting-rule engine) + Dr VAT Input (where creditable), with the credit
/// going either to <b>Cash-Bank</b> (paid) or, when recorded <b>on account</b>, to <b>Accounts Payable</b>
/// for a supplier (creating an AP open item) — all in one transaction (BR-EXP-3).
/// </summary>
public sealed record PostExpenseVoucherCommand(
    DateOnly ExpenseDate, string? PayeeName, Guid? CashAccountId, Guid? SupplierId, DateOnly? DueDate,
    string? Reference, string? Notes,
    IReadOnlyList<ExpenseVoucherLineInput> Lines) : ICommand<Guid>, IIdempotentCommand;

public sealed class PostExpenseVoucherValidator : AbstractValidator<PostExpenseVoucherCommand>
{
    public PostExpenseVoucherValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CashAccountId.HasValue ^ x.SupplierId.HasValue)
            .WithMessage("Provide either a cash/bank account (paid) or a supplier (on account), not both.");
        RuleFor(x => x.DueDate).NotNull().When(x => x.SupplierId.HasValue)
            .WithMessage("An on-account voucher needs a due date.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("An expense voucher requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ExpenseCategoryId).NotEmpty();
            l.RuleFor(x => x.Amount).GreaterThan(0);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0m, 1m);
        });
    }
}

public sealed class PostExpenseVoucherHandler : ICommandHandler<PostExpenseVoucherCommand, Guid>
{
    private readonly IExpenseCategoryRepository _categories;
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;
    private readonly IMasterDataLookup _masterData;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public PostExpenseVoucherHandler(
        IExpenseCategoryRepository categories,
        IExpenseVoucherRepository vouchers,
        ICrossModuleUnitOfWork uow,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts,
        ISubledgerPosting subledger,
        IMasterDataLookup masterData,
        ICompanyDirectory companies,
        ITenantContext tenant)
    {
        _categories = categories;
        _vouchers = vouchers;
        _uow = uow;
        _ledger = ledger;
        _accounts = accounts;
        _subledger = subledger;
        _masterData = masterData;
        _companies = companies;
        _tenant = tenant;
    }

    public Task<Result<Guid>> Handle(PostExpenseVoucherCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostExpenseVoucherCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("EXPENSES.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var sequence = await _vouchers.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new ExpenseVoucherNumberSequence();
            _vouchers.AddSequence(sequence);
        }

        var onAccount = request.SupplierId.HasValue;
        if (onAccount && !await _masterData.SupplierExistsAsync(request.SupplierId!.Value, ct))
        {
            return Error.NotFound("EXPENSES.SUPPLIER_NOT_FOUND", "Supplier not found.");
        }

        var number = sequence.Take(request.ExpenseDate);
        var voucher = onAccount
            ? ExpenseVoucher.CreateOnAccount(
                number, request.ExpenseDate, request.PayeeName, request.SupplierId!.Value, request.DueDate!.Value,
                company.FunctionalCurrency, request.Reference, request.Notes)
            : ExpenseVoucher.CreatePaid(
                number, request.ExpenseDate, request.PayeeName, request.CashAccountId!.Value,
                company.FunctionalCurrency, request.Reference, request.Notes);

        // Resolve each line's expense account via the posting-rule engine and accumulate the debit
        // per account (so a multi-line voucher posts one Dr per distinct expense account).
        var expenseDebits = new Dictionary<Guid, decimal>();
        foreach (var input in request.Lines)
        {
            var category = await _categories.GetByIdAsync(input.ExpenseCategoryId, ct);
            if (category is null || !category.IsActive)
            {
                return ExpenseErrors.LineCategoryNotFound(input.ExpenseCategoryId);
            }

            var account = await _accounts.ResolveAsync("Expense", category.PostingRuleKey, PostingSelector.None, ct);
            if (account.IsFailure)
            {
                return account.Error;
            }

            voucher.AddLine(category.Id, category.PostingRuleKey, input.Description, input.Amount, input.TaxRate);
            expenseDebits[account.Value] = expenseDebits.GetValueOrDefault(account.Value) + input.Amount;
        }

        // Dr Expense per account (net) + Dr VAT Input (total creditable tax) / Cr Cash-Bank (gross).
        var lines = expenseDebits
            .Select(kv => new LedgerLine(kv.Key, Math.Round(kv.Value, 4, MidpointRounding.ToEven), 0m, "Operating expense"))
            .ToList();

        if (voucher.TaxTotal > 0m)
        {
            var vatInput = await _accounts.ResolveAsync("Expense", PostingKeys.VatInput, PostingSelector.None, ct);
            if (vatInput.IsFailure)
            {
                return vatInput.Error;
            }

            lines.Add(new LedgerLine(vatInput.Value, voucher.TaxTotal, 0m, "VAT input (PPN Masukan)"));
        }

        // Credit side: Cr Cash-Bank (paid) or Cr Accounts Payable for the supplier (on account).
        if (onAccount)
        {
            var apControl = await _accounts.ResolveAsync("Expense", PostingKeys.ApControl, PostingSelector.None, ct);
            if (apControl.IsFailure)
            {
                return apControl.Error;
            }

            lines.Add(new LedgerLine(apControl.Value, 0m, voucher.GrandTotal, "Accounts payable", request.SupplierId));
        }
        else
        {
            lines.Add(new LedgerLine(request.CashAccountId!.Value, 0m, voucher.GrandTotal, "Cash / bank"));
        }

        var posting = new LedgerPostingRequest(
            request.ExpenseDate, LedgerSource.Expense, voucher.Id,
            $"Expense voucher {number}", lines);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        voucher.SetJournal(journal.Value);

        if (onAccount)
        {
            var openItem = await _subledger.OpenPayableAsync(
                request.SupplierId!.Value, voucher.Id, number, request.ExpenseDate, request.DueDate!.Value,
                voucher.GrandTotal, ct);
            if (openItem.IsFailure)
            {
                return openItem.Error;
            }

            voucher.SetApOpenItem(openItem.Value);
        }

        _vouchers.Add(voucher);

        return voucher.Id;
    }
}

// --- Queries ---

public sealed record GetExpenseVoucherQuery(Guid Id) : IQuery<ExpenseVoucherDto>;

public sealed class GetExpenseVoucherHandler : IQueryHandler<GetExpenseVoucherQuery, ExpenseVoucherDto>
{
    private readonly IExpenseVoucherRepository _vouchers;
    public GetExpenseVoucherHandler(IExpenseVoucherRepository vouchers) => _vouchers = vouchers;

    public async Task<Result<ExpenseVoucherDto>> Handle(GetExpenseVoucherQuery request, CancellationToken ct)
    {
        var v = await _vouchers.GetByIdAsync(request.Id, ct);
        if (v is null)
        {
            return ExpenseErrors.VoucherNotFound;
        }

        return new ExpenseVoucherDto(
            v.Id, v.Number, v.ExpenseDate, v.PayeeName, v.CashAccountId, v.SupplierId, v.DueDate, v.Currency,
            v.SubTotal, v.TaxTotal, v.GrandTotal, v.JournalEntryId, v.ApOpenItemId, v.Reference, v.Notes,
            v.Lines.Select(l => new ExpenseVoucherLineDto(
                l.ExpenseCategoryId, l.Description, l.Amount, l.TaxRate, l.LineTax, l.LineTotal)).ToList());
    }
}

public sealed record GetExpenseVouchersQuery : IQuery<IReadOnlyList<ExpenseVoucherSummaryDto>>;

public sealed class GetExpenseVouchersHandler : IQueryHandler<GetExpenseVouchersQuery, IReadOnlyList<ExpenseVoucherSummaryDto>>
{
    private readonly IExpenseVoucherRepository _vouchers;
    public GetExpenseVouchersHandler(IExpenseVoucherRepository vouchers) => _vouchers = vouchers;

    public async Task<Result<IReadOnlyList<ExpenseVoucherSummaryDto>>> Handle(GetExpenseVouchersQuery request, CancellationToken ct)
    {
        var items = await _vouchers.ListAsync(ct);
        return Result.Success<IReadOnlyList<ExpenseVoucherSummaryDto>>(items
            .Select(v => new ExpenseVoucherSummaryDto(v.Id, v.Number, v.ExpenseDate, v.PayeeName, v.SupplierId, v.GrandTotal, v.JournalEntryId))
            .ToList());
    }
}
