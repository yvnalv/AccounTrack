using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Features;

public sealed record TrialBalanceLineDto(
    string AccountCode, string AccountName, string AccountType, decimal Debit, decimal Credit, decimal Balance);

public sealed record TrialBalanceDto(
    DateOnly? FromDate, DateOnly? ToDate, decimal TotalDebit, decimal TotalCredit, bool IsBalanced,
    IReadOnlyList<TrialBalanceLineDto> Lines);

/// <summary>Trial balance from posted journal lines (ADR-0008 — derived from the GL, not transactional tables).</summary>
public sealed record GetTrialBalanceQuery(DateOnly? FromDate, DateOnly? ToDate) : IQuery<TrialBalanceDto>;

public sealed class GetTrialBalanceQueryHandler : IQueryHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    private readonly IAccountingReadStore _store;

    public GetTrialBalanceQueryHandler(IAccountingReadStore store) => _store = store;

    public async Task<Result<TrialBalanceDto>> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
    {
        var rows = await _store.GetTrialBalanceAsync(request.FromDate, request.ToDate, cancellationToken);

        var lines = rows
            .Select(r => new TrialBalanceLineDto(r.AccountCode, r.AccountName, r.AccountType, r.Debit, r.Credit, r.Balance))
            .ToList();

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);

        return new TrialBalanceDto(
            request.FromDate, request.ToDate, totalDebit, totalCredit, totalDebit == totalCredit, lines);
    }
}
