using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;
using Xunit;

namespace Nexus.Fms.Tests;

/// <summary>
/// Unit tests for <see cref="RuleEngine"/> — covers FR-05, FR-07 (shadow/disabled), FR-09 predicates.
/// </summary>
public sealed class RuleEngineTests
{
    private static readonly RuleEngine Engine = new(NullLogger<RuleEngine>.Instance);

    private static FraudRule MakeRule(string code, string conditionsJson,
        RuleMode mode = RuleMode.Live, bool isSynchronous = true, int score = 10) =>
        new()
        {
            Code = code, Name = code, ConditionsJson = conditionsJson,
            Mode = mode, IsSynchronous = isSynchronous, Score = score,
            Category = RuleCategory.Amount
        };

    private static IReadOnlyDictionary<string, object?> Facts(params (string key, object? val)[] pairs) =>
        pairs.ToDictionary(p => p.key, p => p.val);

    // FR-05: rule fires when its predicate matches.
    [Fact]
    public void Evaluate_MatchingRule_FiresAndReturnsTriggered()
    {
        var rule = MakeRule("R01", """{"field":"Amount","op":"gt","value":1000}""");
        var facts = Facts(("Amount", (object?)1500m));
        var triggered = Engine.Evaluate([rule], facts, synchronousOnly: true);
        triggered.Should().ContainSingle(r => r.Code == "R01");
    }

    [Fact]
    public void Evaluate_NonMatchingRule_DoesNotFire()
    {
        var rule = MakeRule("R01", """{"field":"Amount","op":"gt","value":1000}""");
        var facts = Facts(("Amount", (object?)500m));
        var triggered = Engine.Evaluate([rule], facts, synchronousOnly: true);
        triggered.Should().BeEmpty();
    }

    // FR-07: disabled rules are never evaluated.
    [Fact]
    public void Evaluate_DisabledRule_IsSkipped()
    {
        var rule = MakeRule("R01", """{"field":"Amount","op":"gt","value":0}""", mode: RuleMode.Disabled);
        var facts = Facts(("Amount", (object?)9999m));
        var triggered = Engine.Evaluate([rule], facts, synchronousOnly: true);
        triggered.Should().BeEmpty();
    }

    // FR-07: shadow rules are evaluated and returned with ShadowOnly=true.
    [Fact]
    public void Evaluate_ShadowRule_FiresWithShadowOnlyFlag()
    {
        var rule = MakeRule("R02", """{"field":"Amount","op":"gt","value":0}""", mode: RuleMode.Shadow);
        var facts = Facts(("Amount", (object?)100m));
        var triggered = Engine.Evaluate([rule], facts, synchronousOnly: true);
        triggered.Should().ContainSingle().Which.ShadowOnly.Should().BeTrue();
    }

    // FR-03: async rules are skipped when synchronousOnly=true.
    [Fact]
    public void Evaluate_AsyncRule_SkippedWhenSynchronousOnly()
    {
        var rule = MakeRule("R03", """{"field":"Amount","op":"gt","value":0}""", isSynchronous: false);
        var facts = Facts(("Amount", (object?)100m));
        var triggered = Engine.Evaluate([rule], facts, synchronousOnly: true);
        triggered.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_AsyncRule_IncludedWhenSynchronousOnlyFalse()
    {
        var rule = MakeRule("R03", """{"field":"Amount","op":"gt","value":0}""", isSynchronous: false);
        var facts = Facts(("Amount", (object?)100m));
        var triggered = Engine.Evaluate([rule], facts, synchronousOnly: false);
        triggered.Should().ContainSingle(r => r.Code == "R03");
    }

    // AND / OR composite predicates (FR-09).
    [Fact]
    public void Evaluate_AndPredicate_RequiresAllConditions()
    {
        var conditionsJson = """
        {
          "op": "and",
          "conditions": [
            {"field": "Amount", "op": "gt", "value": 500},
            {"field": "IsNewDevice", "op": "eq", "value": true}
          ]
        }
        """;
        var rule = MakeRule("R04", conditionsJson);

        // Both match → fires
        Engine.Evaluate([rule], Facts(("Amount", (object?)600m), ("IsNewDevice", (object?)true)), true)
            .Should().ContainSingle();

        // Only one matches → does not fire
        Engine.Evaluate([rule], Facts(("Amount", (object?)600m), ("IsNewDevice", (object?)false)), true)
            .Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_OrPredicate_RequiresAnyCondition()
    {
        var conditionsJson = """
        {
          "op": "or",
          "conditions": [
            {"field": "Amount", "op": "gt", "value": 9000},
            {"field": "IsNewDevice", "op": "eq", "value": true}
          ]
        }
        """;
        var rule = MakeRule("R05", conditionsJson);

        // Only new device matches → fires
        Engine.Evaluate([rule], Facts(("Amount", (object?)100m), ("IsNewDevice", (object?)true)), true)
            .Should().ContainSingle();

        // Neither matches → does not fire
        Engine.Evaluate([rule], Facts(("Amount", (object?)100m), ("IsNewDevice", (object?)false)), true)
            .Should().BeEmpty();
    }
}
