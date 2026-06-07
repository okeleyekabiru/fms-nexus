using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Engine;

/// <summary>A single rule that fired during evaluation, with its risk contribution.</summary>
public sealed record TriggeredRule
{
    public required Guid RuleId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required int Score { get; init; }
    public required RuleCategory Category { get; init; }
    public required bool CannotBeOffset { get; init; }
    public required bool ShadowOnly { get; init; }
}

/// <summary>
/// Evaluates all enabled rules against a transaction's facts and returns those that triggered
/// (FR-05, FR-07, FR-09). Disabled rules are skipped; shadow-mode rules are evaluated and
/// reported but flagged so the scoring engine can exclude them from enforced action (Workflow 4).
/// </summary>
public sealed class RuleEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(ILogger<RuleEngine> logger) => _logger = logger;

    public IReadOnlyList<TriggeredRule> Evaluate(
        IEnumerable<FraudRule> rules,
        IReadOnlyDictionary<string, object?> facts,
        bool synchronousOnly)
    {
        var triggered = new List<TriggeredRule>();

        foreach (var rule in rules)
        {
            if (!rule.IsActive) continue;                    // FR-07: disabled rules excluded
            if (synchronousOnly && !rule.IsSynchronous) continue; // FR-03: async rules run post-transaction

            RulePredicate? predicate;
            try
            {
                predicate = JsonSerializer.Deserialize<RulePredicate>(rule.ConditionsJson, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Rule {Code} has invalid conditions JSON and was skipped", rule.Code);
                continue;
            }

            if (predicate is null) continue;

            if (PredicateEvaluator.Evaluate(predicate, facts))
            {
                triggered.Add(new TriggeredRule
                {
                    RuleId = rule.RuleId,
                    Code = rule.Code,
                    Name = rule.Name,
                    Score = rule.Score,
                    Category = rule.Category,
                    CannotBeOffset = rule.CannotBeOffset,
                    ShadowOnly = rule.Mode == RuleMode.Shadow
                });
            }
        }

        return triggered;
    }
}
