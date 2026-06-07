using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Dto;

/// <summary>The verdict envelope returned to the middleware (Workflow 1, step 5).</summary>
public sealed record ScreeningResponse
{
    public required string TransactionRef { get; init; }
    public required Verdict Verdict { get; init; }
    public required int RiskScore { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public required IReadOnlyList<TriggeredRuleDto> TriggeredRules { get; init; }
    public Guid? AlertId { get; init; }
    public Guid? CaseId { get; init; }

    /// <summary>True when the verdict was produced by the fail-open/fail-closed fallback (FR-04).</summary>
    public bool Bypassed { get; init; }
    public long EvaluationMs { get; init; }
}
