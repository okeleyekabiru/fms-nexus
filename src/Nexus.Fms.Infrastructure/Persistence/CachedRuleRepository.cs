using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Persistence;

/// <summary>
/// Decorator around <see cref="IRuleRepository"/> that caches the active rule set in memory
/// for up to 60 seconds (NFR-03). A cache miss falls through to the underlying DB repository.
///
/// Registration: inject as <c>IRuleRepository</c> with the raw <see cref="RuleRepository"/>
/// passed to the constructor.  The admin API should call <see cref="InvalidateAsync"/> after
/// any rule mutation so the cache does not serve stale rules.
/// </summary>
public sealed class CachedRuleRepository : IRuleRepository
{
    private const string ActiveRulesCacheKey = "fms:active-rules";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IRuleRepository _inner;
    private readonly IMemoryCache    _cache;
    private readonly ILogger<CachedRuleRepository> _logger;

    public CachedRuleRepository(
        IRuleRepository inner,
        IMemoryCache cache,
        ILogger<CachedRuleRepository> logger)
    {
        _inner  = inner;
        _cache  = cache;
        _logger = logger;
    }

    // ── Engine path (cached) ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<FraudRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(ActiveRulesCacheKey, out IReadOnlyList<FraudRule>? cached) && cached is not null)
            return cached;

        var rules = await _inner.GetActiveRulesAsync(ct);
        _cache.Set(ActiveRulesCacheKey, rules, CacheTtl);
        _logger.LogDebug("Active rule set cached ({Count} rules, TTL {Ttl}s)", rules.Count, CacheTtl.TotalSeconds);
        return rules;
    }

    // ── Admin CRUD paths (not cached — always hit DB) ──────────────────────────

    public Task<IReadOnlyList<FraudRule>> GetAllAsync(
        RuleApprovalStatus? approvalStatus = null, CancellationToken ct = default) =>
        _inner.GetAllAsync(approvalStatus, ct);

    public Task<FraudRule?> GetByIdAsync(Guid ruleId, CancellationToken ct = default) =>
        _inner.GetByIdAsync(ruleId, ct);

    public async Task<FraudRule> AddAsync(FraudRule rule, CancellationToken ct = default)
    {
        var result = await _inner.AddAsync(rule, ct);
        InvalidateCache();
        return result;
    }

    public async Task<FraudRule> UpdateAsync(FraudRule rule, CancellationToken ct = default)
    {
        var result = await _inner.UpdateAsync(rule, ct);
        InvalidateCache();
        return result;
    }

    public async Task DisableAsync(Guid ruleId, CancellationToken ct = default)
    {
        await _inner.DisableAsync(ruleId, ct);
        InvalidateCache();
    }

    public async Task<FraudRule> ApproveAsync(Guid ruleId, string approvedBy, CancellationToken ct = default)
    {
        var result = await _inner.ApproveAsync(ruleId, approvedBy, ct);
        InvalidateCache();
        return result;
    }

    public async Task<FraudRule> RejectAsync(
        Guid ruleId, string rejectedBy, string reason, CancellationToken ct = default)
    {
        var result = await _inner.RejectAsync(ruleId, rejectedBy, reason, ct);
        InvalidateCache();
        return result;
    }

    // ── Cache management ───────────────────────────────────────────────────────

    /// <summary>Evict the active rule cache after a mutation.</summary>
    public Task InvalidateAsync()
    {
        InvalidateCache();
        return Task.CompletedTask;
    }

    private void InvalidateCache()
    {
        _cache.Remove(ActiveRulesCacheKey);
        _logger.LogDebug("Active rule cache invalidated");
    }
}
