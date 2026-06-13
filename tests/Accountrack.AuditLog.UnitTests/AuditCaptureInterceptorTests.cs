using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.SharedKernel.Auditing;
using Accountrack.SharedKernel.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accountrack.AuditLog.UnitTests;

public class AuditCaptureInterceptorTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

    private static TestDbContext CreateContext()
    {
        var ctx = new FakeContext(TenantId, CompanyId, UserId, Now);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditCaptureInterceptor(ctx, ctx, ctx))
            .Options;
        return new TestDbContext(options, ctx);
    }

    [Fact]
    public async Task Insert_is_captured_as_an_audit_entry()
    {
        await using var db = CreateContext();
        db.Widgets.Add(new Widget("Bolt") { TenantId = TenantId, CompanyId = CompanyId });
        await db.SaveChangesAsync();

        var audit = await db.Set<AuditEntry>().ToListAsync();
        audit.Should().ContainSingle();
        audit[0].Action.Should().Be(AuditAction.Insert);
        audit[0].EntityType.Should().Be(nameof(Widget));
        audit[0].TenantId.Should().Be(TenantId);
        audit[0].CompanyId.Should().Be(CompanyId);
        audit[0].UserId.Should().Be(UserId);
        audit[0].ChangesJson.Should().Contain("Bolt");
    }

    [Fact]
    public async Task Update_captures_before_and_after_values_of_changed_properties_only()
    {
        await using var db = CreateContext();
        var widget = new Widget("Bolt") { TenantId = TenantId, CompanyId = CompanyId };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        widget.Rename("Nut");
        await db.SaveChangesAsync();

        var update = (await db.Set<AuditEntry>().ToListAsync())
            .Single(a => a.Action == AuditAction.Update);
        update.ChangesJson.Should().Contain("Bolt").And.Contain("Nut");
        update.ChangesJson.Should().Contain("Name");
    }

    // --- Test doubles ---

    private sealed class FakeContext : ITenantContext, ICurrentUser, IClock
    {
        public FakeContext(Guid tenantId, Guid companyId, Guid userId, DateTime now)
        {
            TenantId = tenantId;
            CompanyId = companyId;
            UserId = userId;
            UtcNow = now;
            GrantedCompanyIds = new[] { companyId };
        }

        public Guid TenantId { get; }
        public Guid CompanyId { get; }
        public IReadOnlyCollection<Guid> GrantedCompanyIds { get; }
        public bool IsSet => true;
        public Guid UserId { get; }
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => true;
        public DateTime UtcNow { get; }
    }

    private sealed class Widget : TenantOwnedEntity
    {
        public Widget(string name) => Name = name;

        public string Name { get; private set; }

        public void Rename(string name) => Name = name;
    }

    private sealed class TestDbContext : BaseDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenant)
            : base(options, tenant)
        {
        }

        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Widget>();
            base.OnModelCreating(modelBuilder);
            ApplyAccountrackConventions(modelBuilder);
        }
    }
}
