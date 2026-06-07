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
    Task<FraudRule?> GetByIdAsyn