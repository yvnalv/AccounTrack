using Accountrack.SharedKernel.Domain;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// Per-company gapless counter for journal entry numbers. Incremented within the posting
/// transaction; optimistic concurrency (RowVersion) serializes concurrent posts.
/// </summary>
public sealed class JournalNumberSequence : TenantOwnedEntity
{
    private JournalNumberSequence() { }

    public JournalNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    /// <summary>Returns the current value as a formatted entry number and advances the counter.</summary>
    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"JE/{date.Year:D4}{date.Month:D2}/{value:D6}";
    }
}
