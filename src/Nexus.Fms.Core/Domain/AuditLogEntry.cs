namespace Nexus.Fms.Core.Domain;

/// <summary>
/// Immutable audit trail record for all admin mutations (FR-26).
/// Written by <c>IAuditLogger</c>; never updated or deleted.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Action verb, e.g. "RuleCreated", "RuleApproved", "ListEntryAdded".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Domain type being mutated, e.g. "FraudRule", "ListEntry", "ThresholdBands".</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>JSON snapshot of the entity before the change (null for creates).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot of the entity after the change (null for deletes).</summary>
    public string? NewValues { get; set; }

    public string PerformedBy { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
