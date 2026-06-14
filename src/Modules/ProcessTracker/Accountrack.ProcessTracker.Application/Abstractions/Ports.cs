using Accountrack.ProcessTracker.Domain;

namespace Accountrack.ProcessTracker.Application.Abstractions;

public interface IProcessTimelineRepository
{
    void Add(ProcessEvent processEvent);
    Task<IReadOnlyList<ProcessEvent>> GetTimelineAsync(string documentType, Guid documentId, CancellationToken ct);
}

public interface IProcessTrackerUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
