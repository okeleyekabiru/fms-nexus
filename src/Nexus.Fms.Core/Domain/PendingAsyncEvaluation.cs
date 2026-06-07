namespace Nexus.Fms.Core.Domain;

/// <summary>
/// Outbox record for a transaction that needs post-commit async rule evaluation (FR-03).
/// Written synchronously during screening; consumed by <c>AsyncEvaluationJob</c> out-of-band.
/// </summary>
public class PendingAsyncEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The alert created during the synchronous screening pass.</summary>
    public Guid AlertId { get; set; }

    /// <summary>JSON-serialized <see cref="TransactionContext"/> for replay.</summary>
    public string TransactionContextJson { get; set; } = string.Empty;

    public AsyncEvalStatus Status { get; set; } = AsyncEvalStatus.Pending;

    /// <summary>Error detail when Status = Failed.</summary>
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public enum AsyncEvalStatus
{
    Pending,
    Completed,
    Failed
}
