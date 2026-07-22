using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Accounting.Application.Features;

public sealed record PostJournalLine(Guid AccountId, decimal Debit, decimal Credit, string? Description);

/// <summary>
/// Posts a manual, balanced journal into the active company's books, routed through the Approval
/// Workflow (ADR-0040): auto-approved and posted now when no rule matches, otherwise held for approval.
/// </summary>
public sealed record PostJournalCommand(
    DateOnly Date, string Description, IReadOnlyList<PostJournalLine> Lines)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class PostJournalCommandValidator : AbstractValidator<PostJournalCommand>
{
    public PostJournalCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count >= 2)
            .WithMessage("A journal must have at least two lines.");
        RuleForEach(x => x.Lines).Must(l => (l.Debit > 0) ^ (l.Credit > 0))
            .WithMessage("Each line must be exactly one of debit or credit, and positive.");
    }
}

public sealed class PostJournalCommandHandler : ICommandHandler<PostJournalCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;

    public PostJournalCommandHandler(IManualJournalService service) => _service = service;

    public Task<Result<ManualJournalResult>> Handle(PostJournalCommand request, CancellationToken ct)
    {
        var lines = request.Lines
            .Select(l => new ManualJournalLine(l.AccountId, l.Debit, l.Credit, l.Description))
            .ToList();
        return _service.SubmitAsync(request.Date, request.Description, JournalSource.Manual, lines, ct);
    }
}

/// <summary>The general-journal register (ADR-0040): all non-draft journals over an optional date range.</summary>
public sealed record GetJournalEntriesQuery(DateOnly? FromDate, DateOnly? ToDate)
    : IQuery<IReadOnlyList<JournalRegisterItemDto>>;

public sealed class GetJournalEntriesHandler : IQueryHandler<GetJournalEntriesQuery, IReadOnlyList<JournalRegisterItemDto>>
{
    private readonly IAccountingReadStore _store;

    public GetJournalEntriesHandler(IAccountingReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<JournalRegisterItemDto>>> Handle(GetJournalEntriesQuery request, CancellationToken ct)
    {
        var rows = await _store.GetJournalRegisterAsync(request.FromDate, request.ToDate, ct);
        return Result.Success<IReadOnlyList<JournalRegisterItemDto>>(rows
            .Select(r => new JournalRegisterItemDto(r.Id, r.EntryNo, r.Date, r.Source, r.Status, r.Description, r.Amount))
            .ToList());
    }
}

/// <summary>Reverses a posted journal by posting its mirror image (BR-ACC-3).</summary>
public sealed record ReverseJournalCommand(Guid JournalId, DateOnly? Date, string? Reason) : ICommand<Guid>;

public sealed class ReverseJournalCommandHandler : ICommandHandler<ReverseJournalCommand, Guid>
{
    private readonly IJournalRepository _journals;
    private readonly IJournalPoster _poster;
    private readonly IAccountingUnitOfWork _uow;

    public ReverseJournalCommandHandler(IJournalRepository journals, IJournalPoster poster, IAccountingUnitOfWork uow)
    {
        _journals = journals;
        _poster = poster;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(ReverseJournalCommand request, CancellationToken cancellationToken)
    {
        var original = await _journals.GetByIdAsync(request.JournalId, cancellationToken);
        if (original is null)
        {
            return AccountingErrors.JournalNotFound;
        }

        if (original.Status == JournalStatus.Reversed)
        {
            return AccountingErrors.AlreadyReversed;
        }

        if (original.Status != JournalStatus.Posted)
        {
            return AccountingErrors.JournalNotPosted;
        }

        var date = request.Date ?? original.Date;
        var description = request.Reason ?? $"Reversal of {original.EntryNo}";
        var reversal = original.CreateReversal(date, description);

        var result = await _poster.PostAsync(reversal, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        original.MarkReversedBy(reversal.Id);
        await _uow.SaveChangesAsync(cancellationToken);
        return result.Value;
    }
}

public sealed record GetJournalEntryQuery(Guid Id) : IQuery<JournalEntryDto>;

public sealed class GetJournalEntryQueryHandler : IQueryHandler<GetJournalEntryQuery, JournalEntryDto>
{
    private readonly IJournalRepository _journals;

    public GetJournalEntryQueryHandler(IJournalRepository journals) => _journals = journals;

    public async Task<Result<JournalEntryDto>> Handle(GetJournalEntryQuery request, CancellationToken cancellationToken)
    {
        var entry = await _journals.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
        {
            return AccountingErrors.JournalNotFound;
        }

        return new JournalEntryDto(
            entry.Id, entry.EntryNo, entry.Date, entry.Currency, entry.Status.ToString(), entry.Source.ToString(),
            entry.Description, entry.TotalDebit.Amount, entry.TotalCredit.Amount,
            entry.Lines.Select(l => new JournalLineDto(l.AccountId, l.Debit.Amount, l.Credit.Amount, l.Description)).ToList());
    }
}
