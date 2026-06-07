using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>
/// Aggregated reporting and dashboard queries (FR-22, FR-23, FR-24).
/// Implementations may apply caching; callers must not assume live data.
/// </summary>
public interface IReportingService
{
    /// <summary>High-level summary for the fraud dashboard (FR-22).</summary>
    Task<DashboardSummary> GetDashboardSummaryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>
    /// Alert counts grouped by hour for the heat-map view (FR-23).
    /// Returns one entry per hour within the window; hours with zero alerts are included.
    /// </summary>
    Task<IReadOnlyList<AlertHeatMapPoint>> GetAlertHeatMapAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>
    /// Paginated alert list for the 30-day history view (FR-20 gap, FR-24).
    /// Supports filtering by risk level and verdict.
    /// </summary>
    Task<IReadOnlyList<AlertReportRow>> GetAlertReportAsync(
        DateTimeOffset from, DateTimeOffset to,
        RiskLevel? riskLevel, Verdict? verdict,
        int skip, int take, CancellationToken ct = default);

    /// <summary>Rule effectiveness — how often each rule fires and its average score contribution (FR-22).</summary>
    Task<IReadOnlyList<RuleEffectivenessRow>> GetRuleEffectivenessAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

// ── Report models ──────────────────────────────────────────────────────────────

public sealed record DashboardSummary
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }

    public int TotalAlerts { get; init; }
    public int BlockedTransactions { get; init; }
    public int FlaggedTransactions { get; init; }
    public int MfaRequiredTransactions { get; init; }

    public int OpenCases { get; init; }
    public int EscalatedCases { get; init; }
    public int ResolvedCasesTotal { get; init; }
    public int ConfirmedFraudCases { get; init; }
    public int FalsePositiveCases { get; init; }

    /// <summary>Alert count time series for sparkline (one entry per day).</summary>
    public IReadOnlyList<AlertHeatMapPoint> DailyAlertSeries { get; init; }
        = Array.Empty<AlertHeatMapPoint>();
}

public sealed record AlertHeatMapPoint(DateTimeOffset Hour, int AlertCount, int BlockCount);

public sealed record AlertReportRow
{
    public Guid AlertId { get; init; }
    public string TransactionRef { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public int CompositeRiskScore { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public string Verdict { get; init; } = string.Empty;
    public bool ShadowOnly { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record RuleEffectivenessRow
{
    public string RuleCode { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public int TriggerCount { get; init; }
    public double TriggerRatePct { get; init; }
}
