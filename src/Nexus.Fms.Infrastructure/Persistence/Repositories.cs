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
        rule.Mode = RuleMode.Disabled; // ensure rejected rules are not active
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
        _db.ListEntrie

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
            OldValues   = oldValues is null ? null : System.Text.Json.JsonSerializer.Serialize(oldValues),
            NewValues   = newValues is null ? null : System.Text.Json.JsonSerializer.Serialize(newValues)
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetEntriesAsync(
        string? entityType, Guid? entityId, int skip, int take, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (entityType != null) q = q.Where(e => e.EntityType == entityType);
        if (entityId.HasValue) q = q.Where(e => e.EntityId == entityId.Value);
        return await q.OrderByDescending(e => e.Timestamp).Skip(skip).Take(take).ToListAsync(ct);
    }
}
