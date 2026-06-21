using Accountrack.SharedKernel.Domain;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// A company fiscal year and its (monthly) periods (ADR-0010). Posting is only allowed into an
/// Open period; period close/lock prevents back-dated tampering.
/// </summary>
public sealed class FiscalYear : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<FiscalPeriod> _periods = new();

    private FiscalYear() { }

    private FiscalYear(int year, DateOnly startDate, DateOnly endDate)
    {
        Year = year;
        StartDate = startDate;
        EndDate = endDate;
        IsClosed = false;
    }

    public int Year { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsClosed { get; private set; }

    public IReadOnlyCollection<FiscalPeriod> Periods => _periods.AsReadOnly();

    /// <summary>Creates a fiscal year with 12 monthly periods starting at <paramref name="startMonth"/>.</summary>
    public static FiscalYear Create(int year, int startMonth = 1)
    {
        if (startMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(startMonth), "Start month must be 1-12.");
        }

        var start = new DateOnly(year, startMonth, 1);
        var end = start.AddMonths(12).AddDays(-1);
        var fy = new FiscalYear(year, start, end);

        for (var i = 0; i < 12; i++)
        {
            var pStart = start.AddMonths(i);
            var pEnd = pStart.AddMonths(1).AddDays(-1);
            fy._periods.Add(FiscalPeriod.Create(i + 1, pStart, pEnd));
        }

        return fy;
    }

    public FiscalPeriod? PeriodFor(DateOnly date) =>
        _periods.FirstOrDefault(p => date >= p.StartDate && date <= p.EndDate);

    /// <summary>
    /// Finalizes the year after the closing entry has been posted: marks the year closed and locks
    /// every period so no further postings (back-dated or otherwise) can land in it.
    /// </summary>
    public void Close()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Fiscal year is already closed.");
        }

        IsClosed = true;
        foreach (var period in _periods)
        {
            period.Lock();
        }
    }
}

/// <summary>A single period (month) within a fiscal year.</summary>
public sealed class FiscalPeriod : TenantOwnedEntity, IAggregateRoot
{
    private FiscalPeriod() { }

    private FiscalPeriod(int periodNo, DateOnly startDate, DateOnly endDate)
    {
        PeriodNo = periodNo;
        StartDate = startDate;
        EndDate = endDate;
        Status = FiscalPeriodStatus.Open;
    }

    public Guid FiscalYearId { get; private set; }
    public int PeriodNo { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalPeriodStatus Status { get; private set; }

    internal static FiscalPeriod Create(int periodNo, DateOnly startDate, DateOnly endDate) =>
        new(periodNo, startDate, endDate);

    public bool IsOpen => Status == FiscalPeriodStatus.Open;

    public void Close()
    {
        if (Status == FiscalPeriodStatus.Locked)
        {
            throw new InvalidOperationException("A locked period cannot be closed again.");
        }

        Status = FiscalPeriodStatus.Closed;
    }

    public void Reopen()
    {
        if (Status == FiscalPeriodStatus.Locked)
        {
            throw new InvalidOperationException("A locked period cannot be reopened.");
        }

        Status = FiscalPeriodStatus.Open;
    }

    public void Lock() => Status = FiscalPeriodStatus.Locked;
}
