using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Contracts;

// ── Request bodies ─────────────────────────────────────────────────────────────

public sealed record AssignCaseRequest(string AnalystId);

public sealed record AddNoteRequest(string Text);

public sealed record ResolveCaseRequest(
    CaseResolution Resolution,
    Verdict? VerdictOverride = null);

// ── Response DTOs ──────────────────────────────────────────────────────────────

public sealed record FraudCaseDto
{
    public Guid CaseId { get; init; }
    public Guid AlertId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? AssignedTo { get; init; }
    public string? Resolution { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string? SarReference { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset? LastEscalatedAt { get; init; }

    /// <summary>Summary of the triggering alert, included on single-case GET.</summary>
    public FraudAlertSummaryDto? Alert { get; init; }

    public static FraudCaseDto From(FraudCase c, FraudAlert? alert = null) => new()
    {
        CaseId           = c.CaseId,
        AlertId          = c.AlertId,
        Status           = c.Status.ToString(),
        AssignedTo       = c.AssignedTo,
        Resolution       = c.Resolution?.ToString(),
        Notes            = c.Notes,
        SarReference     = c.SarReference,
        CreatedAt        = c.CreatedAt,
        ResolvedAt       = c.ResolvedAt,
        LastEscalatedAt  = c.LastEscalatedAt,
        Alert            = alert is not null ? FraudAlertSummaryDto.From(alert) : null
    };
}

public sealed record FraudAlertSummaryDto
{
    public Guid AlertId { get; init; }
    public string TransactionRef { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public int CompositeRiskScore { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public string Verdict { get; init; } = string.Empty;
    public bool ShadowOnly { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public static FraudAlertSummaryDto From(FraudAlert a) => new()
    {
        AlertId            = a.AlertId,
        TransactionRef     = a.TransactionRef,
        CustomerId         = a.CustomerId,
        CompositeRiskScore = a.CompositeRiskScore,
        RiskLevel          = a.RiskLevel.ToString(),
        Verdict            = a.Verdict.ToString(),
        ShadowOnly         = a.ShadowOnly,
        CreatedAt          = a.CreatedAt
    };
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Skip, int Take);
