using Microsoft.EntityFrameworkCore;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Persistence;

public sealed class RuleRepository : IRuleRepository
{
    private readonly FmsDbContext _db;
    public RuleRepository(FmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<FraudRule>> GetActiveRulesAsync(CancellationToken ct = default) =>
        await _db.Rules.AsNoTracking()
            .Where(r => r.Mode == RuleMode.Live || r.Mode == RuleMode.Shadow)
            .ToListAsync(ct);
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
        CaseStat