using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Read/write access to fraud cases and alerts (FR-19, FR-20).</summary>
public interface ICaseRepository
{
    Task<FraudCase?> GetByIdAsync(Guid caseId, CancellationToken ct = default);
    Task<FraudAlert?> GetAlertByIdAsync(Guid alertId, CancellationToken ct = default);

    Task<IReadOnlyList<FraudCase>> ListAsync(
        CaseStatus? status,
        string? assignedTo,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>Cases that have been New or UnderInvestigation for longer than <paramref name="olderThan"/> (FR-21).</summary>
    Task<IReadOnlyList<FraudCase>> GetStaleAsync(DateTimeOffset olderThan, CancellationToken ct = default);

    Task<FraudCase> SaveAsync(FraudCase fraudCase, CancellationToken ct = default);
}
