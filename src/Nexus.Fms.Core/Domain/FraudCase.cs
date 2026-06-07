namespace Nexus.Fms.Core.Domain;

/// <summary>
/// A fraud case, created for any transaction scoring at or above the P3 threshold (FR-18),
/// following the lifecycle New → Under Investigation → Escalated → Resolved (FR-19).
/// </summary>
public class FraudCase
{
    public Guid CaseId { get; set; } = Guid.NewGuid();
    public Guid AlertId { get; set; }

    public CaseStatus Status { get; set; } = CaseStatus.New;
    public string? AssignedTo { get; set; }
    public CaseResolution? Resolution { get; set; }

    /// <summary>Append-only investigation notes (FR-20).</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>NIBSS SAR reference, populated when a Suspicious Activity Report is filed (FR-17).</summary>
    public string? SarReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Drives 24h auto-escalation (FR-21) and 72h Head-of-Ops alert (Workflow 3).</summary>
    public DateTimeOffset? LastEscalatedAt { get; set; }
}
