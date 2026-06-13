using System.Reflection;
using Accountrack.Application.Abstractions.Context;
using Accountrack.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Infrastructure.Common.Persistence;

/// <summary>
/// Base <see cref="DbContext"/> for every module. Applies the cross-cutting conventions that
/// make the platform safe by default (docs/DATABASE.md, MULTI_TENANCY.md):
///   - a global query filter excluding soft-deleted rows on every <see cref="Entity"/>;
///   - an additional tenant + company filter on every <see cref="TenantOwnedEntity"/>;
///   - an optimistic-concurrency rowversion on every entity;
///   - ignoring the in-memory domain-event buffer.
/// Derived module contexts call <see cref="ApplyAccountrackConventions"/> from OnModelCreating.
/// </summary>
public abstract class BaseDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    protected BaseDbContext(DbContextOptions options, ITenantContext tenant) : base(options) =>
        _tenant = tenant;

    /// <summary>Exposed so the compiled global query filters resolve the tenant per query.</summary>
    public Guid CurrentTenantId => _tenant.TenantId;

    public Guid CurrentCompanyId => _tenant.CompanyId;

    protected void ApplyAccountrackConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;

            if (!typeof(Entity).IsAssignableFrom(clr))
            {
                continue;
            }

            // The in-memory event buffer is not persisted.
            modelBuilder.Entity(clr).Ignore(nameof(Entity.DomainEvents));

            // Optimistic concurrency token (ADR-0021).
            modelBuilder.Entity(clr).Property(nameof(Entity.RowVersion)).IsRowVersion();

            // Global query filters (most specific first).
            if (typeof(TenantOwnedEntity).IsAssignableFrom(clr))
            {
                InvokeGeneric(nameof(ApplyTenantOwnedFilter), clr, modelBuilder);
            }
            else if (typeof(TenantScopedEntity).IsAssignableFrom(clr))
            {
                InvokeGeneric(nameof(ApplyTenantScopedFilter), clr, modelBuilder);
            }
            else
            {
                InvokeGeneric(nameof(ApplySoftDeleteFilter), clr, modelBuilder);
            }
        }
    }

    private void InvokeGeneric(string methodName, Type clrType, ModelBuilder modelBuilder)
    {
        typeof(BaseDbContext)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(clrType)
            .Invoke(this, new object[] { modelBuilder });
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : Entity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void ApplyTenantScopedFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : TenantScopedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted
            && e.TenantId == CurrentTenantId);
    }

    private void ApplyTenantOwnedFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : TenantOwnedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted
            && e.TenantId == CurrentTenantId
            && e.CompanyId == CurrentCompanyId);
    }
}
