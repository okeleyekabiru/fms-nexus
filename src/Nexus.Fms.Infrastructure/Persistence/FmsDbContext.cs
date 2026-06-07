using Microsoft.EntityFrameworkCore;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Persistence;

/// <summary>EF Core context for the FMS data model (section 6). Targets PostgreSQL (JSONB columns).</summary>
public class FmsDbContext : DbContext
{
    public FmsDbContext(DbContextOptions<FmsDbContext> options) : base(options) { }

    public DbSet<FraudRule> Rules => Set<FraudRule>();
    public DbSet<FraudAlert> Alerts => Set<FraudAlert>();
    public DbSet<FraudCase> Cases => Set<FraudCase>();
    public DbSet<ListEntry> ListEntries => Set<ListEntry>();
    public DbSet<PendingAsyncEvaluation> AsyncEvaluations => Set<PendingAsyncEvaluation>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FraudRule>(e =>
        {
            e.ToTable("fraud_rules");
            e.HasKey(x => x.RuleId);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ConditionsJson).HasColumnType("jsonb");
            e.Property(x => x.Category).HasConversion<string>();
            e.Property(x => x.Mode).HasConversion<string>();
            e.Property(x => x.ApprovalStatus).HasConversion<string>();
        });

        b.Entity<FraudAlert>(e =>
        {
            e.ToTable("fraud_alerts");
            e.HasKey(x => x.AlertId);
            e.HasIndex(x => x.TransactionRef);
            e.HasIndex(x => x.CustomerId);
            e.Property(x => x.TriggeredRulesJson).HasColumnType("jsonb");
            e.Property(x => x.NibssLookupResultJson).HasColumnType("jsonb");
            e.Property(x => x.RiskLevel).HasConversion<string>();
            e.Property(x => x.Verdict).HasConversion<string>();
        });

        b.Entity<FraudCase>(e =>
        {
            e.ToTable("fraud_cases");
            e.HasKey(x => x.CaseId);
            e.HasIndex(x => x.AlertId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Resolution).HasConversion<string>();
        });

        b.Entity<ListEntry>(e =>
        {
            e.ToTable("fraud_list_entries");
            e.HasKey(x => x.EntryId);
            e.HasIndex(x => x.Bvn);
            e.HasInd