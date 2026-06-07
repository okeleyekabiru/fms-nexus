using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>
/// Orchestrates the fraud analyst case workflow (FR-19, FR-20, Workflow 3).
/// All mutations are append-only in their audit trail and fire side-effects
/// (e.g. SAR submission on ConfirmedFraud) through <see cref="ICaseSideEffectsHandler"/>.
/// </summary>
public interface ICaseManagementService
{
    Task<FraudCase> AssignAsync(Guid caseId, string analystId, CancellationToken ct = default);

    /// <summary>Appends a timestamped note to the case (FR-20, append-only).</summary>
    Task<FraudCase> AddNoteAsync(Guid caseId, string note, string authorId, CancellationToken ct = default);

    Task<FraudCase> EscalateAsync(Guid caseId, string escalatedBy, CancellationToken ct = default);

    /// <summary>
    /// Resolves the case. If resolution is <see cref="CaseResolution.ConfirmedFraud"/>,
    /// fires the SAR + auto-blacklist side effect (FR-17, Workflow 5).
    /// </summary>
    Task<FraudCase> ResolveAsync(
        Guid caseId,
        CaseResolution resolution,
        string resolvedBy,
        CancellationToken ct = default);

    /// <summary>Allows an analyst to override the system verdict (FR-20).</summary>
    Task<FraudCase> OverrideVerdictAsync(
        Guid caseId,
        Verdict newVerdict,
        string overriddenBy,
        CancellationToken ct = default);
}
