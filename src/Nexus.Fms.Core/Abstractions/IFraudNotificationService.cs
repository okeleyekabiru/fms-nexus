using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>
/// Sends operational notifications for critical fraud events (Workflow 3, step 6 — 72h alert).
/// Implementations can target email, Teams webhook, PagerDuty, etc.
/// A stub implementation logs a critical message (no external dependency required in dev).
/// </summary>
public interface IFraudNotificationService
{
    /// <summary>Notify the Head of Operations that a case has been unresolved beyond the SLA.</summary>
    Task NotifyHeadOfOpsAsync(FraudCase fraudCase, string reason, CancellationToken ct = default);

    /// <summary>Notify analysts that a new high-priority case has been created (P1/P2).</summary>
    Task NotifyNewHighPriorityCaseAsync(FraudCase fraudCase, FraudAlert alert, CancellationToken ct = default);
}
