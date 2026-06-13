using Accountrack.Application.Abstractions.Context;

namespace Accountrack.Identity.UnitTests;

/// <summary>Deterministic clock for tests (CODING_STANDARDS.md §3 / TESTING.md §3).</summary>
internal sealed class TestClock : IClock
{
    public TestClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}
