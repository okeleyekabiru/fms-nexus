using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Provides the active rule set to the engine and full CRUD for the admin API (FR-08, NFR-07, FR-25).</summary>
public interface IRuleRepository
{
    // ── Engine path ────────────────────────────────────────────────────────────
    Task<IReadOnlyList<FraudRule>> GetActiveRulesAsync(CancellationToken ct = default);

    // ── Admin CRUD ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<FraudRule>> GetAllAsync(RuleApprovalStatus? approvalStatus = null, CancellationToken ct = default);
    Task<FraudRule?> GetByIdAsync(Guid ruleId, CancellationToken ct = default);
    Task<FraudRule> AddAsync(FraudRule rule, CancellationToken ct = default);
    Task<FraudRule> UpdateAsync(FraudRule rule, CancellationToken ct = default);

    /// <summary>Soft-delete: sets Mode = Disabled. Never hard-deletes (audit trail).</summary>
    Task DisableAsync(Guid ruleId, CancellationToken ct = default);

    // ── Maker-checker (FR-25) ──────────────────────────────────────────────────
    Task<FraudRule> ApproveAsync(Guid ruleId, string approvedBy, CancellationToken ct = default);
    Task<FraudRule> RejectAsync(Guid ruleId, string rejectedBy, string reason, CancellationToken ct = default);
}

/// <summary>Result of a NIBSS Fraud Bureau watchlist lookup (FR-14–FR-16).</summary>
public sealed record NibssLookupResult
{
    public bool OnWatchlist { get; init; }
    public bool ConfirmedFraud { get; init; }
    /// <summary>True when the lookup could not be completed (timeout/unavailable) — FR-16.</summary>
    public bool Unavailable { get; init; }
    public string? RawResponse { get; init; }

    public static NibssLookupResult NotFound { get; } = new();
    public static NibssLookupResult Failed { get; } = new() { Unavailable = true };
}
