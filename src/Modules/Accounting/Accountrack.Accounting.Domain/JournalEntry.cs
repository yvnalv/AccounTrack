using Accountrack.SharedKernel.Domain;
using Accountrack.SharedKernel.ValueObjects;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// A double-entry journal (ADR-0009). Built as a draft, then posted once balanced. Posted entries
/// are immutable — corrections are made by posting a reversing entry.
/// </summary>
public sealed class JournalEntry : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<JournalLine> _lines = new();

    private JournalEntry() { }

    private JournalEntry(DateOnly date, string currency, JournalSource source, Guid? sourceDocumentId, string description)
    {
        Date = date;
        Currency = currency;
        Source = source;
        SourceDocumentId = sourceDocumentId;
        Description = description;
        Status = JournalStatus.Draft;
    }

    /// <summary>The gapless per-company number, assigned only when the entry is posted (null while a
    /// manual journal is a draft or awaiting approval — ADR-0040).</summary>
    public string? EntryNo { get; private set; }
    public DateOnly Date { get; private set; }
    public string Currency { get; private set; } = default!;
    public Guid? FiscalPeriodId { get; private set; }
    public JournalSource Source { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public string Description { get; private set; } = default!;
    public JournalStatus Status { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public Guid? ReversedByEntryId { get; private set; }

    /// <summary>The approval request raised for a manual journal / guided flow (null when auto-approved
    /// or for automatic postings from other modules — ADR-0040).</summary>
    public Guid? ApprovalRequestId { get; private set; }

    /// <summary>A manual journal submitted and waiting for approval; not yet in the GL.</summary>
    public bool IsAwaitingApproval => Status == JournalStatus.PendingApproval;

    public IReadOnlyCollection<JournalLine> Lines => _lines.AsReadOnly();

    public static JournalEntry CreateDraft(
        DateOnly date, string currency, JournalSource source, Guid? sourceDocumentId, string description) =>
        new(date, currency, source, sourceDocumentId, description);

    public void AddLine(Guid accountId, decimal debit, decimal credit, string? description = null, Guid? subledgerPartyId = null)
    {
        if (Status != JournalStatus.Draft)
        {
            throw new InvalidOperationException("Lines can only be added to a draft journal.");
        }

        _lines.Add(JournalLine.Create(
            accountId, Money.Create(debit, Currency), Money.Create(credit, Currency), description, subledgerPartyId));
    }

    public Money TotalDebit => _lines.Aggregate(Money.Zero(Currency), (sum, l) => sum.Add(l.Debit));

    public Money TotalCredit => _lines.Aggregate(Money.Zero(Currency), (sum, l) => sum.Add(l.Credit));

    public bool IsBalanced => TotalDebit.Round() == TotalCredit.Round();

    /// <summary>Submits a manual-journal draft for approval; it stays out of the GL until posted (ADR-0040).</summary>
    public void SubmitForApproval(Guid approvalRequestId)
    {
        if (Status != JournalStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft journal can be submitted for approval.");
        }

        ApprovalRequestId = approvalRequestId;
        Status = JournalStatus.PendingApproval;
    }

    /// <summary>Rejects a manual journal that was awaiting approval; it is never posted (ADR-0040).</summary>
    public void MarkRejected()
    {
        if (Status != JournalStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only a journal awaiting approval can be rejected.");
        }

        Status = JournalStatus.Rejected;
    }

    /// <summary>Validates double-entry invariants (BR-ACC-1/2) and marks the entry posted. Accepts a
    /// fresh draft (auto-approved / automatic posting) or a journal that was awaiting approval (ADR-0040).</summary>
    public void Post(string entryNo, Guid fiscalPeriodId, DateTime nowUtc)
    {
        if (Status is not (JournalStatus.Draft or JournalStatus.PendingApproval))
        {
            throw new InvalidOperationException("Only a draft or pending-approval journal can be posted.");
        }

        if (_lines.Count < 2)
        {
            throw new InvalidOperationException("A journal must have at least two lines.");
        }

        if (!IsBalanced)
        {
            throw new InvalidOperationException(
                $"Journal is not balanced: debit {TotalDebit} != credit {TotalCredit}.");
        }

        EntryNo = entryNo;
        FiscalPeriodId = fiscalPeriodId;
        PostedAtUtc = nowUtc;
        Status = JournalStatus.Posted;
    }

    /// <summary>Builds a reversing draft (debits/credits swapped) for a posted entry (BR-ACC-3).</summary>
    public JournalEntry CreateReversal(DateOnly date, string description)
    {
        if (Status != JournalStatus.Posted)
        {
            throw new InvalidOperationException("Only a posted journal can be reversed.");
        }

        var reversal = new JournalEntry(date, Currency, Source, SourceDocumentId, description)
        {
            ReversesEntryId = Id,
        };

        foreach (var line in _lines)
        {
            reversal.AddLine(line.AccountId, line.Credit.Amount, line.Debit.Amount, line.Description, line.SubledgerPartyId);
        }

        return reversal;
    }

    public void MarkReversedBy(Guid reversalEntryId)
    {
        ReversedByEntryId = reversalEntryId;
        Status = JournalStatus.Reversed;
    }
}

/// <summary>A single journal line: debit XOR credit, non-negative (BR-ACC-2).</summary>
public sealed class JournalLine : Entity
{
    private JournalLine() { }

    private JournalLine(Guid accountId, Money debit, Money credit, string? description, Guid? subledgerPartyId)
    {
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Description = description;
        SubledgerPartyId = subledgerPartyId;
    }

    public Guid JournalEntryId { get; private set; }
    public Guid AccountId { get; private set; }
    public Money Debit { get; private set; } = default!;
    public Money Credit { get; private set; } = default!;
    public string? Description { get; private set; }

    /// <summary>Customer/supplier reference for control-account lines (AR/AP subledger — slice 2).</summary>
    public Guid? SubledgerPartyId { get; private set; }

    internal static JournalLine Create(
        Guid accountId, Money debit, Money credit, string? description, Guid? subledgerPartyId = null)
    {
        if (debit.Amount < 0 || credit.Amount < 0)
        {
            throw new InvalidOperationException("Journal line amounts must be non-negative.");
        }

        var debitPositive = debit.Amount > 0;
        var creditPositive = credit.Amount > 0;
        if (debitPositive == creditPositive)
        {
            throw new InvalidOperationException("A journal line must be exactly one of debit or credit.");
        }

        return new JournalLine(accountId, debit, credit, description, subledgerPartyId);
    }
}
