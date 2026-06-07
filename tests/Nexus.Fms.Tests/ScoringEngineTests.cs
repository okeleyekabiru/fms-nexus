using FluentAssertions;
using Microsoft.Extensions.Options;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;
using Nexus.Fms.Core.Scoring;
using Xunit;

namespace Nexus.Fms.Tests;

/// <summary>
/// Unit tests for <see cref="ScoringEngine"/> — covers FR-10, FR-11, FR-12, FR-15.
/// </summary>
public sealed class ScoringEngineTests
{
    private static ScoringEngine CreateEngine(int p1 = 75, int p2 = 50, int p3 = 25, int p4 = 1) =>
        new(Options.Create(new ThresholdBands { P1Min = p1, P2Min = p2, P3Min = p3, P4Min = p4 }));

    private static TriggeredRule MakeRule(int score, bool cannotBeOffset = false, bool shadowOnly = false) =>
        new()
        {
            RuleId = Guid.NewGuid(), Code = "R00", Name = "Test", Score = score,
            Category = RuleCategory.Amount, CannotBeOffset = cannotBeOffset, ShadowOnly = shadowOnly
        };

    // FR-10: composite score is sum of triggered rule scores, floored at 0.
    [Fact]
    public void Score_WithNoRules_ReturnsClean()
    {
        var result = CreateEngine().Score([]);
        result.CompositeScore.Should().Be(0);
        result.RiskLevel.Should().Be(RiskLevel.Clean);
        result.Verdict.Should().Be(Verdict.Allow);
    }

    [Fact]
    public void Score_WithPositiveRules_SumsCorrectly()
    {
        var rules = new[] { MakeRule(30), MakeRule(20) };
        var result = CreateEngine().Score(rules);
        result.CompositeScore.Should().Be(50);
        result.RiskLevel.Should().Be(RiskLevel.P2);
        result.Verdict.Should().Be(Verdict.RequireMfa);
    }

    // FR-10: floor at 0 — negative mitigants cannot push score below 0.
    [Fact]
    public void Score_NegativeMitigant_CannotProduceNegativeScore()
    {
        var rules = new[] { MakeRule(10), MakeRule(-50) };
        var result = CreateEngine().Score(rules);
        result.CompositeScore.Should().Be(0);
        result.RiskLevel.Should().Be(RiskLevel.Clean);
    }

    // FR-15: CannotBeOffset rules are added on top of the floored subtotal.
    [Fact]
    public void Score_CannotBeOffsetRule_AddsOnTopOfFlooredSubtotal()
    {
        // offsettable part: 10 - 50 = floored to 0
        // cannot-be-offset: +80 added on top of 0 → total 80
        var rules = new[]
        {
            MakeRule(10),
            MakeRule(-50),
            MakeRule(80, cannotBeOffset: true)
        };
        var result = CreateEngine().Score(rules);
        result.CompositeScore.Should().Be(80);
        result.RiskLevel.Should().Be(RiskLevel.P1);
        result.Verdict.Should().Be(Verdict.Block);
    }

    // FR-11: exact boundary — score at P1Min should be P1.
    [Theory]
    [InlineData(75, RiskLevel.P1, Verdict.Block)]
    [InlineData(74, RiskLevel.P2, Verdict.RequireMfa)]
    [InlineData(50, RiskLevel.P2, Verdict.RequireMfa)]
    [InlineData(49, RiskLevel.P3, Verdict.Flag)]
    [InlineData(25, RiskLevel.P3, Verdict.Flag)]
    [InlineData(24, RiskLevel.P4, Verdict.Allow)]
    [InlineData(1,  RiskLevel.P4, Verdict.Allow)]
    [InlineData(0,  RiskLevel.Clean, Verdict.Allow)]
    public void Score_ThresholdBoundaries_ClassifyCorrectly(int score, RiskLevel expected, Verdict expectedVerdict)
    {
        var rules = score > 0 ? new[] { MakeRule(score) } : Array.Empty<TriggeredRule>();
        var result = CreateEngine().Score(rules);
        result.CompositeScore.Should().Be(score);
        result.RiskLevel.Should().Be(expected);
        result.Verdict.Should().Be(expectedVerdict);
    }

    // Shadow-mode rules fire and appear in ShadowRules but don't affect enforced score.
    [Fact]
    public void Score_ShadowRules_NotCountedInEnforcedScore()
    {
        var rules = new[]
        {
            MakeRule(30),                           // live → contributes to enforced score
            MakeRule(80, shadowOnly: true)           // shadow → does NOT
        };
        var result = CreateEngine().Score(rules);
        result.CompositeScore.Should().Be(30);       // only live rule
        result.ShadowCompositeScore.Should().Be(110); // both rules combined
        result.ShadowRules.Should().HaveCount(1);
        result.EffectiveRules.Should().HaveCount(1);
    }
}
