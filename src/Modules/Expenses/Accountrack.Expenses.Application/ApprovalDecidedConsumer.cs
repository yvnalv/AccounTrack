using Accountrack.Application.Abstractions.Integration;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Features;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Expenses.Application;

/// <summary>
/// Posts (or rejects) an expense voucher when its approval is decided (event-driven, ADR-0007). On
/// approval it runs the same atomic GL posting as the auto-approved path; on rejection it marks the
/// voucher rejected. Best-effort eventual consumer — a posting failure leaves the voucher pending for
/// retry (no durable outbox yet).
/// </summary>
public sealed class ApprovalDecidedConsumer : IIntegrationEventHandler<ApprovalDecided>
{
    private readonly IExpenseVoucherRepository _vouchers;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IExpensesUnitOfWork _expensesUow;
    private readonly IExpenseVoucherPoster _poster;

    public ApprovalDecidedConsumer(
        IExpenseVoucherRepository vouchers, ICrossModuleUnitOfWork uow,
        IExpensesUnitOfWork expensesUow, IExpenseVoucherPoster poster)
    {
        _vouchers = vouchers;
        _uow = uow;
        _expensesUow = expensesUow;
        _poster = poster;
    }

    public async Task HandleAsync(ApprovalDecided e, CancellationToken ct)
    {
        if (e.DocumentType != ExpenseDocumentTypes.ExpenseVoucher)
        {
            return;
        }

        var voucher = await _vouchers.GetByIdAsync(e.DocumentId, ct);
        if (voucher is null || !voucher.IsPendingApproval)
        {
            return; // unknown, already posted, or already rejected — idempotent no-op
        }

        if (e.Approved && e.Status == "Approved")
        {
            // Post the GL atomically (voucher status + journal + AP open item).
            await _uow.ExecuteAsync<bool>(async token =>
            {
                var posted = await _poster.PostAsync(voucher, token);
                return posted.IsFailure ? posted.Error : Result.Success(true);
            }, ct);
        }
        else if (!e.Approved)
        {
            voucher.MarkRejected();
            await _expensesUow.SaveChangesAsync(ct);
        }
        // else: a multi-level approval advanced but isn't final — nothing to do yet.
    }
}
