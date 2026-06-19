using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Features;

public sealed record VatReportDto(
    DateOnly? FromDate,
    DateOnly? ToDate,
    string OutputAccountCode,
    string OutputAccountName,
    decimal OutputTax,
    string InputAccountCode,
    string InputAccountName,
    decimal InputTax,
    decimal NetVatPayable);

/// <summary>
/// Indonesian PPN report for a period (ADR-0012): VAT Output (PPN Keluaran, collected on sales)
/// minus VAT Input (PPN Masukan, paid on purchases). Net &gt; 0 is payable to the tax office; net
/// &lt; 0 is an overpayment carried forward. Derived from the GL (posted journal lines), with the VAT
/// accounts resolved from the posting-rule engine so it follows configuration, not hardcoded codes.
/// </summary>
public sealed record GetVatReportQuery(DateOnly? FromDate, DateOnly? ToDate) : IQuery<VatReportDto>;

public sealed class GetVatReportQueryHandler : IQueryHandler<GetVatReportQuery, VatReportDto>
{
    private readonly IPostingRuleResolver _resolver;
    private readonly IAccountingReadStore _store;
    private readonly IAccountRepository _accounts;

    public GetVatReportQueryHandler(
        IPostingRuleResolver resolver, IAccountingReadStore store, IAccountRepository accounts)
    {
        _resolver = resolver;
        _store = store;
        _accounts = accounts;
    }

    public async Task<Result<VatReportDto>> Handle(GetVatReportQuery request, CancellationToken cancellationToken)
    {
        var output = await _resolver.ResolveAsync(
            PostingRule.AnyEvent, PostingRuleKeys.VatOutput, PostingSelector.None, cancellationToken);
        if (output.IsFailure)
        {
            return Result.Failure<VatReportDto>(output.Error);
        }

        var input = await _resolver.ResolveAsync(
            PostingRule.AnyEvent, PostingRuleKeys.VatInput, PostingSelector.None, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<VatReportDto>(input.Error);
        }

        var movements = await _store.GetAccountMovementsAsync(
            new[] { output.Value, input.Value }, request.FromDate, request.ToDate, cancellationToken);
        var accounts = await _accounts.GetByIdsAsync(new[] { output.Value, input.Value }, cancellationToken);

        var outputMove = movements.FirstOrDefault(m => m.AccountId == output.Value);
        var inputMove = movements.FirstOrDefault(m => m.AccountId == input.Value);

        // Output is a liability (increases on credit); Input is an asset (increases on debit).
        var outputTax = (outputMove?.Credit ?? 0m) - (outputMove?.Debit ?? 0m);
        var inputTax = (inputMove?.Debit ?? 0m) - (inputMove?.Credit ?? 0m);

        accounts.TryGetValue(output.Value, out var outputAccount);
        accounts.TryGetValue(input.Value, out var inputAccount);

        return new VatReportDto(
            request.FromDate,
            request.ToDate,
            outputAccount?.Code ?? "",
            outputAccount?.Name ?? "",
            outputTax,
            inputAccount?.Code ?? "",
            inputAccount?.Name ?? "",
            inputTax,
            outputTax - inputTax);
    }
}
