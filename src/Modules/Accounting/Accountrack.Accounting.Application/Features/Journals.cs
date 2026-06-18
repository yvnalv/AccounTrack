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

/// <summary>Posts a manual, balanced journal entry into the active company's books.</summary>
public sealed record PostJournalCommand(
    DateOnly Date, string Description, IReadOnlyList<PostJournalLine> Lines) : ICommand<Guid>, IIdempotentCommand;

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

public sealed class PostJournalCommandHandler : ICommandHandler<PostJournalCommand, Guid>
{
    private readonly IJournalPoster _poster;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IAccountingUnitOfWork _uow;

    public PostJournalCommandHandler(
        IJournalPoster poster, ICompanyDirectory companies, ITenantContext tenant, IAccountingUnitOfWork uow)
    {
        _poster = poster;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(PostJournalCommand request, CancellationToken cancellationToken)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("ACCOUNTING.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var draft = JournalEntry.CreateDraft(
            request.Date, company.FunctionalCurrency, JournalSource.Manual, null, request.Description.Trim());

        foreach (var line in request.Lines)
        {
            draft.AddLine(line.AccountId, line.Debit, line.Credit, line.Description);
        }

        var result = await _poster.PostAsync(draft, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return result.Value;
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
