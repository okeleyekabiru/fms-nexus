using Microsoft.Extensions.Options;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;

namespace Nexus.Fms.Core.Scoring;

/// <summary>Outcome of scoring a set of triggered rules.</summary>
public sealed record ScoringResult
{
    public required int CompositeScore { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public required Verdict Verdict { get; init; }

    /// <summary>Rules that actually contributed to the enforced score (live rules only).</summary>
    public required IReadOnlyList<TriggeredRule> EffectiveRules { get; init; }

    /// <summary>Shadow-mode rules that fired but did not affect the enforced action (Workflow 4).</summary>
    public required IReadOnlyList<TriggeredRule> ShadowRules { get; init; }

    /// <summary>The composite score including shadow rules — used for tuning/reporting only.</summary>
    public required int ShadowCompositeScore { get; init; }
}

/// <summary>
/// Computes the composite risk score from triggered rules and maps it to a verdict (FR-10–FR-13).
///
/// Scoring rules:
///  - Composite = sum of triggered rule scores (positive add risk, negative reduce). (FR-10)
///  - Floored at 0 — never negative. (FR-10)
///  - "CannotBeOffset" rules (R09 internal blacklist, NIBSS confirmed fraud) are summed AFTER
///    the offsettable subtotal is floored at 0, so negative mitigants can never reduce them. (FR-15, R09)
///  - The floored composite is classified into a risk band and mapped to an action. (FR-11)
/// </summary>
public sealed class ScoringEngine
{
    private readonly ThresholdBands _bands;

    public ScoringEngine(IOptions<ThresholdBands> bands)
    {
        _bands = bands.Value;
        _bands.Validate();
    }

    public ScoringResult Score(IReadOnlyList<TriggeredRule> triggered)
    {
        var live = triggered.Where(r => !r.ShadowOnly).ToList();
        var shadow = triggered.Where(r => r.ShadowOnly).ToList();

        var enforced = ComputeComposite(live);
        var shadowComposite = ComputeComposite(triggered); // live + shadow combined, for tuning

        var level = _bands.Classify(enforced);

        return new ScoringResult
        {
            CompositeScore = enforced,
            RiskLevel = level,
            Verdict = MapVerdict(level),
            EffectiveRules = live,
            ShadowRules = shadow,
            ShadowCompositeScore = shadowComposite
        };
    }

    private static int ComputeComposite(IReadOnlyList<TriggeredRule> rules)
    {
        // Offsettable subtotal can be reduced by negative mitigants, floored at 0.
        var offsettable = rules.Where(r => !r.CannotBeOffset).Sum(r => r.Score);
        if (offsettable < 0) offsettable = 0; // FR-10: minimum 0

        // Non-offsettable contributions are added on top and cannot be cancelled (FR-15, R09).
        var nonOffsettable = rules.Where(r => r.CannotBeOffset).Sum(r => Math.Max(0, r.Score));

        return offsettable + nonOffsettable;
    }

    private static Verdict MapVerdict(RiskLevel level) => level switch
    {
        RiskLevel.P1 => Verdict.Block,        // AUTO-BLOCK
        RiskLevel.P2 => Verdict.RequireMfa,   // REQUIRE MFA
        RiskLevel.P3 => Verdict.Flag,         // FLAG FOR REVIEW
        RiskLevel.P4 => Verdict.Allow,        // LOG ONLY (proceeds)
        _ => Verdict.Allow                    // Clean
    };
}
