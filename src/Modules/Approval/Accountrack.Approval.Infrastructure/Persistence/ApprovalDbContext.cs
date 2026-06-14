using Accountrack.Application.Abstractions.Context;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Domain;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Approval.Infrastructure.Persistence;

/// <summary>EF Core context owning the Approval schema ("approval"). Module unit of work.</summary>
public sealed class ApprovalDbContext : BaseDbContext, IApprovalUnitOfWork
{
    public const string Schema = "approval";

    public ApprovalDbContext(DbContextOptions<ApprovalDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<ApprovalDefinition> Definitions => Set<ApprovalDefinition>();
    public DbSet<ApprovalRequest> Requests => Set<ApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ApprovalDefinition>(b =>
        {
            b.ToTable("ApprovalDefinitions");
            b.Property(d => d.DocumentType).IsRequired().HasMaxLength(64);
            b.Property(d => d.Name).IsRequired().HasMaxLength(128);
            b.HasMany(d => d.Conditions).WithOne().HasForeignKey(c => c.ApprovalDefinitionId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(d => d.Conditions).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(d => d.Steps).WithOne().HasForeignKey(s => s.ApprovalDefinitionId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(d => d.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(d => new { d.TenantId, d.CompanyId, d.DocumentType });
        });

        modelBuilder.Entity<ApprovalCondition>(b =>
        {
            b.ToTable("ApprovalConditions");
            b.Property(c => c.Attribute).IsRequired().HasMaxLength(64);
            b.Property(c => c.Operator).HasConversion<int>();
            b.Property(c => c.Value).HasColumnType("decimal(19,4)");
        });

        modelBuilder.Entity<ApprovalStep>(b =>
        {
            b.ToTable("ApprovalSteps");
            b.Property(s => s.ApproverType).HasConversion<int>();
            b.Property(s => s.ApproverRef).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<ApprovalRequest>(b =>
        {
            b.ToTable("ApprovalRequests");
            b.Property(r => r.DocumentType).IsRequired().HasMaxLength(64);
            b.Property(r => r.Status).HasConversion<int>();
            b.HasMany(r => r.Steps).WithOne().HasForeignKey(s => s.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(r => r.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(r => r.Actions).WithOne().HasForeignKey(a => a.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(r => r.Actions).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(r => new { r.TenantId, r.CompanyId, r.DocumentType, r.DocumentId });
            b.HasIndex(r => new { r.TenantId, r.CompanyId, r.Status });
        });

        modelBuilder.Entity<ApprovalRequestStep>(b =>
        {
            b.ToTable("ApprovalRequestSteps");
            b.Property(s => s.ApproverType).HasConversion<int>();
            b.Property(s => s.ApproverRef).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<ApprovalAction>(b =>
        {
            b.ToTable("ApprovalActions");
            b.Property(a => a.Decision).HasConversion<int>();
            b.Property(a => a.Comment).HasMaxLength(512);
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}
