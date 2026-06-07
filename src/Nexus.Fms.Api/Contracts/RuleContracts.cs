using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Contracts;

// ── Requests ───────────────────────────────────────────────────────────────────

public sealed record CreateRuleRequest(
    string Code,
    string Name,
    string Description,
    RuleCategory Category,
    string ConditionsJson,
    int Score,
    bool IsSynchronous = true,
    bool CannotBeOffset = false);

public sealed record UpdateRuleRequest(
    string Name,
    string Description,
    RuleCategory Category,
    string ConditionsJson,
    int Score,
    RuleMode Mode,
    bool IsSynchronous = true,
    bool CannotBeOffset = false);

public sealed record SetRuleModeRequest(RuleMode Mode);

public sealed record RejectRuleRequest(string Reason);

// ── Response DTOs ──────────────────────────────────────────────────────────────

public sealed record FraudRuleDto
{
    public Guid RuleId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ConditionsJson { get; init; } = string.Empty;
    public int Score { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public bool IsSynchronous { get; init; }
    public bool CannotBeOffset { get; init; }
    public string? CreatedBy { get; init; }
    public string? ApprovedBy { get; init; }
    public string? RejectedBy { get; init; }
    public string? RejectionReason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public static FraudRuleDto From(FraudRule r) => new()
    {
        RuleId          = r.RuleId,
        Code            = r.Code,
        Name            = r.Name,
        Description     = r.Description,
        Category        = r.Category.ToString(),
        ConditionsJson  = r.ConditionsJson,
        Score           = r.Score,
        Mode            = r.Mode.ToString(),
        ApprovalStatus  = r.ApprovalStatus.ToString(),
        IsSynchronous   = r.IsSynchronous,
        CannotBeOffset  = r.CannotBeOffset,
        CreatedBy       = r.CreatedBy,
        ApprovedBy      = r.ApprovedBy,
        RejectedBy      = r.RejectedBy,
        RejectionReason = r.RejectionReason,
        CreatedAt       = r.CreatedAt,
        UpdatedAt       = r.UpdatedAt
    };
}

public sealed record AuditLogEntryDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string PerformedBy { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }

    public static AuditLogEntryDto From(Core.Domain.AuditLogEntry e) => new()
    {
        Id          = e.Id,
        Action      = e.Action,
        EntityType  = e.EntityType,
        EntityId    = e.EntityId,
        OldValues   = e.OldValues,
        NewValues   = e.NewValues,
        PerformedBy = e.PerformedBy,
        Timestamp   = e.Timestamp
    };
}
