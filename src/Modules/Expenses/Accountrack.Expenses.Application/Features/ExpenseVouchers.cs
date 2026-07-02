using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Contracts;
using Accountrack.Expenses.Domain;
using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Expenses.Application.Features;

public sealed record ExpenseVoucherLineInput(Guid ExpenseCategoryId, string? Description, decimal Amount, decimal TaxRate);

/// <summary>
/// Records an operating-expense voucher and routes it through approval (ADR-0030, BR-EXP-5). It is
/// submitted to the approval engine: when no rule matches it is <b>auto-approved and posted
/// immediately</b> (Dr Expense per category + Dr VAT Input / Cr Cash-Bank|AP, atomically — BR-EXP-3);
/// when a rule matches it waits as <b>PendingApproval</b> and is posted by the approval consumer once
/// approved.
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
    private readonly IExpensesUnitOfWork _expensesUow;
    private readonly IExpenseVoucherPoster _poster;
    private readonly IApprovalService _approval;
    private readonly IMasterDataLookup _masterData;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public PostExpenseVoucherHandler(
        IExpenseCategoryRepository categories,
        IExpenseVoucherRepository vouchers,
        ICrossModuleUnitOfWork uow,
        IExpensesUnitOfWork expensesUow,
        IExpenseVoucherPoster poster,
        IApprovalService approval,
        IMasterDataLookup masterData,
        ICompanyDirectory companies,
        ITenantContext tenant)
    {
        _categories = categories;
        _vouchers = vouchers;
        _uow = uow;
        _expensesUow = expensesUow;
        _poster = poster;
        _approval = approval;
        _masterData = masterData;
        _companies = companies;
        _tenant = tenant;
    }

    public async Task<Result<Guid>> Handle(PostExpenseVoucherCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("EXPENSES.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var onAccount = request.SupplierId.HasValue;
        if (onAccount && !await _masterData.SupplierExistsAsync(request.SupplierId!.Value, ct))
        {
            return Error.NotFound("EXPENSES.SUPPLIER_NOT_FOUND", "Supplier not found.");
        }

        var sequence = await _vouchers.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new ExpenseVoucherNumberSequence();
            _vouchers.AddSequence(sequence);
        }

        var number = sequence.Take(request.ExpenseDate);
        var voucher = onAccount
            ? ExpenseVoucher.CreateOnAccount(
                number, request.ExpenseDate, request.PayeeName, request.SupplierId!.Value, request.DueDate!.Value,
                company.FunctionalCurrency, request.Reference, request.Notes)
            : ExpenseVoucher.CreatePaid(
                number, request.ExpenseDate, request.PayeeName, request.CashAccountId!.Value,
                company.FunctionalCurrency, request.Reference, request.Notes);

        foreach (var input in request.Lines)
        {
            var category = await _categories.GetByIdAsync(input.ExpenseCategoryId, ct);
            if (category is null || !category.IsActive)
            {
                return ExpenseErrors.LineCategoryNotFound(input.ExpenseCategoryId);
            }

            voucher.AddLine(category.Id, category.PostingRuleKey, input.Description, input.Amount, input.TaxRate);
        }

        // Route through the approval engine: a matching rule (e.g. amount threshold) holds the voucher
        // for approval; otherwise it auto-approves and posts now (BR-EXP-5).
        var submission = await _approval.SubmitAsync(
            ExpenseDocumentTypes.ExpenseVoucher, voucher.Id,
            new Dictionary<string, decimal> { ["Total"] = voucher.GrandTotal }, ct);

        if (submission.Status != "AutoApproved")
        {
            voucher.MarkPendingApproval(submission.RequestId);
            _vouchers.Add(voucher);
            await _expensesUow.SaveChangesAsync(ct);
            return voucher.Id;
        }

        // Auto-approved: record + post atomically (voucher, GL journal, AP open item).
        return await _uow.ExecuteAsync(async token =>
        {
            _vouchers.Add(voucher);
            var posted = await _poster.PostAsync(voucher, token);
            return posted.IsFailure ? posted.Error : Result.Success(voucher.Id);
        }, ct);
    }
}

// --- Draft workflow (create → edit → submit → cancel) and reversal (full parity with Sales/Purchasing) ---

/// <summary>Shared line assembly + header validation reused across the create/update commands.</summary>
internal static class ExpenseVoucherAssembler
{
    public static async Task<Result> AddLinesAsync(
        ExpenseVoucher voucher, IReadOnlyList<ExpenseVoucherLineInput> lines,
        IExpenseCategoryRepository categories, CancellationToken ct)
    {
        foreach (var input in lines)
        {
            var category = await categories.GetByIdAsync(input.ExpenseCategoryId, ct);
            if (category is null || !category.IsActive)
            {
                return ExpenseErrors.LineCategoryNotFound(input.ExpenseCategoryId);
            }

            voucher.AddLine(category.Id, category.PostingRuleKey, input.Description, input.Amount, input.TaxRate);
        }

        return Result.Success();
    }
}

/// <summary>Creates an expense voucher as a <b>Draft</b> — editable and reviewable before it posts.</summary>
public sealed record CreateExpenseDraftCommand(
    DateOnly ExpenseDate, string? PayeeName, Guid? CashAccountId, Guid? SupplierId, DateOnly? DueDate,
    string? Reference, string? Notes,
    IReadOnlyList<ExpenseVoucherLineInput> Lines) : ICommand<Guid>, IIdempotentCommand;

public sealed class CreateExpenseDraftValidator : AbstractValidator<CreateExpenseDraftCommand>
{
    public CreateExpenseDraftValidator()
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

public sealed class CreateExpenseDraftHandler : ICommandHandler<CreateExpenseDraftCommand, Guid>
{
    private readonly IExpenseCategoryRepository _categories;
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly IExpensesUnitOfWork _expensesUow;
    private readonly IMasterDataLookup _masterData;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public CreateExpenseDraftHandler(
        IExpenseCategoryRepository categories, IExpenseVoucherRepository vouchers, IExpensesUnitOfWork expensesUow,
        IMasterDataLookup masterData, ICompanyDirectory companies, ITenantContext tenant)
    {
        _categories = categories;
        _vouchers = vouchers;
        _expensesUow = expensesUow;
        _masterData = masterData;
        _companies = companies;
        _tenant = tenant;
    }

    public async Task<Result<Guid>> Handle(CreateExpenseDraftCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("EXPENSES.COMPANY_NOT_FOUND", "Active company not found.");
        }

        if (request.SupplierId.HasValue && !await _masterData.SupplierExistsAsync(request.SupplierId.Value, ct))
        {
            return Error.NotFound("EXPENSES.SUPPLIER_NOT_FOUND", "Supplier not found.");
        }

        var sequence = await _vouchers.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new ExpenseVoucherNumberSequence();
            _vouchers.AddSequence(sequence);
        }

        var number = sequence.Take(request.ExpenseDate);
        var voucher = request.SupplierId.HasValue
            ? ExpenseVoucher.CreateOnAccount(
                number, request.ExpenseDate, request.PayeeName, request.SupplierId.Value, request.DueDate!.Value,
                company.FunctionalCurrency, request.Reference, request.Notes)
            : ExpenseVoucher.CreatePaid(
                number, request.ExpenseDate, request.PayeeName, request.CashAccountId!.Value,
                company.FunctionalCurrency, request.Reference, request.Notes);

        var linesResult = await ExpenseVoucherAssembler.AddLinesAsync(voucher, request.Lines, _categories, ct);
        if (linesResult.IsFailure)
        {
            return linesResult.Error;
        }

        _vouchers.Add(voucher);
        await _expensesUow.SaveChangesAsync(ct);
        return voucher.Id;
    }
}

/// <summary>Edits a draft voucher's header and lines. Rejected once the voucher has left Draft.</summary>
public sealed record UpdateExpenseVoucherCommand(
    Guid Id, DateOnly ExpenseDate, string? PayeeName, Guid? CashAccountId, Guid? SupplierId, DateOnly? DueDate,
    string? Reference, string? Notes,
    IReadOnlyList<ExpenseVoucherLineInput> Lines, byte[]? RowVersion) : ICommand;

public sealed class UpdateExpenseVoucherValidator : AbstractValidator<UpdateExpenseVoucherCommand>
{
    public UpdateExpenseVoucherValidator()
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

public sealed class UpdateExpenseVoucherHandler : ICommandHandler<UpdateExpenseVoucherCommand>
{
    private readonly IExpenseCategoryRepository _categories;
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly IExpensesUnitOfWork _expensesUow;
    private readonly IMasterDataLookup _masterData;

    public UpdateExpenseVoucherHandler(
        IExpenseCategoryRepository categories, IExpenseVoucherRepository vouchers,
        IExpensesUnitOfWork expensesUow, IMasterDataLookup masterData)
    {
        _categories = categories;
        _vouchers = vouchers;
        _expensesUow = expensesUow;
        _masterData = masterData;
    }

    public async Task<Result> Handle(UpdateExpenseVoucherCommand request, CancellationToken ct)
    {
        var voucher = await _vouchers.GetByIdAsync(request.Id, ct);
        if (voucher is null)
        {
            return ExpenseErrors.VoucherNotFound;
        }

        if (!voucher.CanEdit)
        {
            return ExpenseErrors.NotDraft;
        }

        if (request.SupplierId.HasValue && !await _masterData.SupplierExistsAsync(request.SupplierId.Value, ct))
        {
            return Error.NotFound("EXPENSES.SUPPLIER_NOT_FOUND", "Supplier not found.");
        }

        voucher.EditDraft(
            request.ExpenseDate, request.PayeeName, request.CashAccountId, request.SupplierId, request.DueDate,
            request.Reference, request.Notes);

        var linesResult = await ExpenseVoucherAssembler.AddLinesAsync(voucher, request.Lines, _categories, ct);
        if (linesResult.IsFailure)
        {
            return linesResult.Error;
        }

        await _expensesUow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Submits a draft voucher through the approval engine: no matching rule → auto-approved and posted
/// now (atomically); a matching rule → held as PendingApproval and posted by the approval consumer.
/// Returns the resulting status ("Posted" or "Pending").
/// </summary>
public sealed record SubmitExpenseVoucherCommand(Guid Id) : ICommand<string>, IIdempotentCommand;

public sealed class SubmitExpenseVoucherHandler : ICommandHandler<SubmitExpenseVoucherCommand, string>
{
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IExpensesUnitOfWork _expensesUow;
    private readonly IExpenseVoucherPoster _poster;
    private readonly IApprovalService _approval;

    public SubmitExpenseVoucherHandler(
        IExpenseVoucherRepository vouchers, ICrossModuleUnitOfWork uow, IExpensesUnitOfWork expensesUow,
        IExpenseVoucherPoster poster, IApprovalService approval)
    {
        _vouchers = vouchers;
        _uow = uow;
        _expensesUow = expensesUow;
        _poster = poster;
        _approval = approval;
    }

    public async Task<Result<string>> Handle(SubmitExpenseVoucherCommand request, CancellationToken ct)
    {
        var voucher = await _vouchers.GetByIdAsync(request.Id, ct);
        if (voucher is null)
        {
            return ExpenseErrors.VoucherNotFound;
        }

        if (voucher.Status != ExpenseVoucherStatus.Draft)
        {
            return ExpenseErrors.NotDraft;
        }

        if (voucher.Lines.Count == 0)
        {
            return ExpenseErrors.NoLines;
        }

        var submission = await _approval.SubmitAsync(
            ExpenseDocumentTypes.ExpenseVoucher, voucher.Id,
            new Dictionary<string, decimal> { ["Total"] = voucher.GrandTotal }, ct);

        if (submission.Status != "AutoApproved")
        {
            voucher.MarkPendingApproval(submission.RequestId);
            await _expensesUow.SaveChangesAsync(ct);
            return "Pending";
        }

        return await _uow.ExecuteAsync(async token =>
        {
            var posted = await _poster.PostAsync(voucher, token);
            return posted.IsFailure ? posted.Error : Result.Success("Posted");
        }, ct);
    }
}

/// <summary>Discards a draft voucher (never posted).</summary>
public sealed record CancelExpenseVoucherCommand(Guid Id) : ICommand;

public sealed class CancelExpenseVoucherHandler : ICommandHandler<CancelExpenseVoucherCommand>
{
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly IExpensesUnitOfWork _expensesUow;

    public CancelExpenseVoucherHandler(IExpenseVoucherRepository vouchers, IExpensesUnitOfWork expensesUow)
    {
        _vouchers = vouchers;
        _expensesUow = expensesUow;
    }

    public async Task<Result> Handle(CancelExpenseVoucherCommand request, CancellationToken ct)
    {
        var voucher = await _vouchers.GetByIdAsync(request.Id, ct);
        if (voucher is null)
        {
            return ExpenseErrors.VoucherNotFound;
        }

        if (voucher.Status != ExpenseVoucherStatus.Draft)
        {
            return ExpenseErrors.NotDraft;
        }

        voucher.Cancel();
        await _expensesUow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Reverses a posted voucher (BR-EXP-4): posts a mirror journal that offsets the original, settles a
/// fully-unpaid AP open item, and moves the voucher to Reversed. The original journal stays intact
/// (posted documents are immutable — corrections are reversal-only). Returns the reversed voucher id.
/// </summary>
public sealed record ReverseExpenseVoucherCommand(Guid Id, DateOnly? Date, string? Reason)
    : ICommand<Guid>, IIdempotentCommand;

public sealed class ReverseExpenseVoucherHandler : ICommandHandler<ReverseExpenseVoucherCommand, Guid>
{
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IExpenseVoucherPoster _poster;
    private readonly IClock _clock;

    public ReverseExpenseVoucherHandler(
        IExpenseVoucherRepository vouchers, ICrossModuleUnitOfWork uow, IExpenseVoucherPoster poster, IClock clock)
    {
        _vouchers = vouchers;
        _uow = uow;
        _poster = poster;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(ReverseExpenseVoucherCommand request, CancellationToken ct)
    {
        var voucher = await _vouchers.GetByIdAsync(request.Id, ct);
        if (voucher is null)
        {
            return ExpenseErrors.VoucherNotFound;
        }

        if (!voucher.CanReverse)
        {
            return ExpenseErrors.NotPosted;
        }

        var reversalDate = request.Date ?? DateOnly.FromDateTime(_clock.UtcNow);

        return await _uow.ExecuteAsync(async token =>
        {
            var reversed = await _poster.ReverseAsync(voucher, reversalDate, request.Reason, token);
            return reversed.IsFailure ? reversed.Error : Result.Success(voucher.Id);
        }, ct);
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
            v.SubTotal, v.TaxTotal, v.GrandTotal, v.JournalEntryId, v.ApOpenItemId, v.ReversalJournalEntryId,
            v.Status.ToString(), v.Reference, v.Notes,
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
            .Select(v => new ExpenseVoucherSummaryDto(v.Id, v.Number, v.ExpenseDate, v.PayeeName, v.SupplierId, v.GrandTotal, v.JournalEntryId, v.Status.ToString()))
            .ToList());
    }
}
