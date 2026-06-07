namespace Nexus.Fms.Core.Domain;

/// <summary>
/// A configurable fraud detection rule (§6 fraud_rules, FR-05–FR-08).
/// Conditions are stored as structured JSON predicates (ConditionsJson) so that
/// rules can be created/edited via the Admin Console without code deployment (NFR-07).
/// </summary>
public class FraudRule
{
    public Guid RuleId { get; set; } = Guid.NewGuid();

    /// <summary>Stable short code (e.g. "R01") used in seeds and reporting.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public RuleCategory Category { get; set; }

    /// <summary>Structured predicate definition. See <see cref="Engine.RulePredicate"/>.</summary>
    public string ConditionsJson { get; set; } = "{}";

    /// <summary>
    /// Risk contribution, integer in range -100..+100 (FR-06).
    /// Positive increases risk; negative reduces it.
    /// </summary>
    public int Score { get; set; }

    public RuleMode Mode { get; set; } = RuleMode.Disabled;

    /// <summary>TRUE = evaluated synchronously before the transaction; FALSE = post-transaction (FR-03).</summary>
    public bool IsSynchronous { get; set; } = true;

    /// <summary>
    /// When true the rule's positive score cannot be offset by negative (mitigant) rules
    /// (e.g. R09 internal blacklist, R03 confirmed-fraud watchlist) per FR-15 / R09 notes.
    /// </summary>
    public bool CannotBeOffset { get; set; }

    // Maker-checker audit (FR-25, FR-26)
    public string? CreatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsActive => Mode is RuleMode.Live or RuleMode.Shadow;
}
