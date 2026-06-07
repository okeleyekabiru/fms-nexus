using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using EntityState = Microsoft.EntityFrameworkCore.EntityState;

namespace Nexus.Fms.Infrastructure.Persistence;

public sealed class RuleRepository : IRuleRepository
{
    private readonly FmsDbContext _db;
    public RuleRepository(FmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<FraudRule>> GetActiveRulesAsync(CancellationToken ct = default) =>
        await _db.Rules.AsNoTracking()
            .Where(r => r.Mode == RuleMode.Live || r.Mode == RuleMode.Shadow)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FraudRule>> GetAllAsync(
        RuleApprovalStatus? approvalStatus = null, CancellationToken ct = default)
    {
        var q = _db.Rules.AsNoTracking().AsQueryable();
        if (approvalStatus.HasValue) q = q.Where(r => r.ApprovalStatus == approvalStatus.Value);
        return await q.OrderBy(r => r.Code).ToListAsync(ct);
    }

    public Task<FraudRule?> GetByIdAsync(Guid ruleId, CancellationToken ct = default) =>
        _db.Rules.AsNoTracking().FirstOrDefaultAsync(r => r.RuleId == ruleId, ct);

    public async Task<FraudRule> AddAsync(FraudRule rule, CancellationToken ct = default)
    {
        _db.Rules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<FraudRule> UpdateAsync(FraudRule rule, CancellationToken ct = default)
    {
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        if (_db.Entry(rule).State == EntityState.Detached)
            _db.Rules.Update(rule);
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DisableAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await _db.Rules.FindAsync(new object[] { ruleId }, ct);
        if (rule is null) return;
        rule.Mode = RuleMode.Disabled;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<FraudRule> ApproveAsync(Guid ruleId, string approvedBy, CancellationToken ct = default)
    {
        var rule = await _db.Rules.FindAsync(new object[] { ruleId }, ct)
            ?? throw new KeyNotFoundException($"Rule {ruleId} not found");
        rule.ApprovalStatus = RuleApprovalStatus.Approved;
        rule.ApprovedBy = approvedBy;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<FraudRule> RejectAsync(
        Guid ruleId, string rejectedBy, string reason, CancellationToken ct = default)
    {
        var rule = await _db.Rules.FindAsync(new object[] { ruleId }, ct)
            ?? throw new KeyNotFoundException($"Rule {ruleId} not found");
        rule.ApprovalStatus = RuleApprovalStatus.Rejected;
        rule.RejectedBy = rejectedBy;
        rule.RejectionReason = reason;
        rule.Mode = RuleMode.Disabled;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return rule;
    }
}

public sealed class ListRepository : IListRepository
{
    private readonly FmsDbContext _db;
    public ListRepository(FmsDbContext db) => _db = db;

    public Task<bool> IsWhitelistedAsync(string? bvn, string? account, CancellationToken ct = default) =>
        MatchAsync(ListType.Whitelist, bvn, account, ct);

    public Task<bool> IsBlacklistedAsync(string? bvn, string? account, CancellationToken ct = default) =>
        MatchAsync(ListType.Blacklist, bvn, account, ct);

    private Task<bool> MatchAsync(ListType type, string? bvn, string? account, CancellationToken ct) =>
        _db.ListEntries.AsNoTracking().AnyAsync(e =>
            e.ListType == type &&
            ((bvn != null && e.Bvn == bvn) || (account != null && e.AccountNumber == account)), ct);

    public async Task AddAsync(ListEntry entry, CancellationToken ct = default)
    {
        _db.ListEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ListEntry>> GetEntriesAsync(
        ListType? listType, int skip, int take, CancellationToken ct = default)
    {
        var q = _db.ListEntries.AsNoTracking().AsQueryable();
        if (listType.HasValue) q = q.Where(e => e.ListType == listType.Value);
        return await q.OrderByDescending(e => e.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<ListEntry?> GetByIdAsync(Guid entryId, CancellationToken ct = default) =>
        _db.ListEntries.AsNoTracking().FirstOrDefaultAsync(e => e.EntryId == entryId, ct);

    public async Task RemoveAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _db.ListEntries.FindAsync(new object[] { entryId }, ct);
        if (entry is not null)
        {
            _db.ListEntries.Remove(entry);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public sealed class AlertStore : IAlertStore
{
    private readonly FmsDbContext _db;
    public AlertStore(FmsDbContext db) => _db = db;

    public async Task<FraudAlert> SaveAlertAsync(FraudAlert alert, CancellationToken ct = default)
    {
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<FraudCase> CreateCaseAsync(FraudCase fraudCase, CancellationToken ct = default)
    {
        _db.Cases.Add(fraudCase);
        await _db.SaveChangesAsync(ct);
        return fraudCase;
    }

    public Task<FraudAlert?> GetAlertByIdAsync(Guid alertId, CancellationToken ct = default) =>
        _db.Alerts.FirstOrDefaultAsync(a => a.AlertId == alertId, ct);

    public async Task UpdateAlertAsync(FraudAlert alert, CancellationToken ct = default)
    {
        if (_db.Entry(alert).State == EntityState.Detached)
            _db.Alerts.Update(alert);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class CaseRepository : ICaseRepository
{
    private readonly FmsDbContext _db;
    public CaseRepository(FmsDbContext db) => _db = db;

    public Task<FraudCase?> GetByIdAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId, ct);

    public Task<FraudAlert?> GetAlertByIdAsync(Guid alertId, CancellationToken ct = default) =>
        _db.Alerts.AsNoTracking().FirstOrDefaultAsync(a => a.AlertId == alertId, ct);

    public async Task<IReadOnlyList<FraudCase>> ListAsync(
        CaseStatus? status, string? assignedTo, int skip, int take, CancellationToken ct = default)
    {
        var q = _db.Cases.AsNoTracking().AsQueryable();
        if (status.HasValue)    q = q.Where(c => c.Status == status.Value);
        if (assignedTo != null) q = q.Where(c => c.AssignedTo == assignedTo);
        return await q.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FraudCase>> GetStaleAsync(DateTimeOffset olderThan, CancellationToken ct = default) =>
        await _db.Cases
            .Where(c => (c.Status == CaseStatus.New || c.Status == CaseStatus.UnderInvestigation)
                        && c.CreatedAt < olderThan
                        && (c.LastEscalatedAt == null || c.LastEscalatedAt < olderThan))
            .ToListAsync(ct);

    public async Task<FraudCase> SaveAsync(FraudCase fraudCase, CancellationToken ct = default)
    {
        if (_db.Entry(fraudCase).State == EntityState.Detached)
            _db.Cases.Update(fraudCase);
        await _db.SaveChangesAsync(ct);
        return fraudCase;
    }
}

public sealed class AsyncEvaluationQueue : IAsyncEvaluationQueue
{
    private readonly FmsDbContext _db;
    public AsyncEvaluationQueue(FmsDbContext db) => _db = db;

    public async Task EnqueueAsync(Guid alertId, TransactionContext context, CancellationToken ct = default)
    {
        _db.AsyncEvaluations.Add(new PendingAsyncEvaluation
        {
            AlertId = alertId,
            TransactionContextJson = JsonSerializer.Serialize(context)
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PendingAsyncEvaluation>> DequeueAsync(int batchSize = 20, CancellationToken ct = default) =>
        await _db.AsyncEvaluations
            .Where(e => e.Status == AsyncEvalStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkCompletedAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.AsyncEvaluations.FindAsync(new object[] { id }, ct);
        if (e is null) return;
        e.Status = AsyncEvalStatus.Completed;
        e.ProcessedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        var e = await _db.AsyncEvaluations.FindAsync(new object[] { id }, ct);
        if (e is null) return;
        e.Status = AsyncEvalStatus.Failed;
        e.Error = error;
        e.ProcessedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class AuditLogRepository : IAuditLogger
{
    private readonly FmsDbContext _db;
    public AuditLogRepository(FmsDbContext db) => _db = db;

    public async Task LogAsync(
        string action, string entityType, Guid? entityId, string performedBy,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLogEntry
        {
            Action      = action,
            EntityType  = entityType,
            EntityId    = entityId,
            PerformedBy = performedBy,
            OldValues   = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues   = newValues is null ? null : JsonSerializer.Serialize(newValues)
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetEntriesAsync(
        string? entityType, Guid? entityId, int skip, int take, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (entityType != null)  q = q.Where(e => e.EntityType == entityType);
        if (entityId.HasValue)   q = q.Where(e => e.EntityId == entityId.Value);
        return await q.OrderByDescending(e => e.Timestamp).Skip(skip).Take(take).ToListAsync(ct);
    }
}
