using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Notifications;

/// <summary>
/// Default notification service: emits structured log entries that ops tooling
/// (Datadog, CloudWatch Logs Insights, etc.) can route to PagerDuty or email.
///
/// Replace with a real implementation (email, Teams webhook, PagerDuty) by registering
/// a different <see cref="IFraudNotificationService"/> in DI.
/// </summary>
public sealed class LoggingFraudNotificationService : IFraudNotificationService
{
    private readonly ILogger<LoggingFraudNotificationService> _logger;

    public LoggingFraudNotificationService(ILogger<LoggingFraudNotificationService> logger)
        => _logger = logger;

    public Task NotifyHeadOfOpsAsync(FraudCase fraudCase, string reason, CancellationToken ct = default)
    {
        _logger.LogCritical(
            "HeadOfOpsAlert: Case {CaseId} (AlertId={AlertId}) has been unresolved — {Reason}. " +
            "Status={Status}, CreatedAt={CreatedAt:O}. Immediate action required.",
            fraudCase.CaseId, fraudCase.AlertId, reason,
            fraudCase.Status, fraudCase.CreatedAt);

        return Task.CompletedTask;
    }

    public Task NotifyNewHighPriorityCaseAsync(FraudCase fraudCase, FraudAlert alert, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "HighPriorityCaseAlert: New {RiskLevel} case {CaseId} created for transaction {TxRef}. " +
            "Score={Score}, Verdict={Verdict}.",
            alert.RiskLevel, fraudCase.CaseId, alert.TransactionRef,
            alert.CompositeRiskScore, alert.Verdict);

        return Task.CompletedTask;
    }
}
