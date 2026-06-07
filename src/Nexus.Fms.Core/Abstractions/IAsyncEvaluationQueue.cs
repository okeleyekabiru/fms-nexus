using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>
/// Outbox queue for deferred async rule evaluation (FR-03).
/// Implementations must be safe for concurrent producer (screening path) and
/// consumer (background job) access.
/// </summary>
public interface IAsyncEvaluationQueue
{
    /// <summary>Enqueue a transaction for async rule evaluation after the alert is persisted.</summary>
    Task EnqueueAsync(Guid alertId, TransactionContext context, CancellationToken ct = default);

    /// <summary>Claim and return up to <paramref name="batchSize"/> pending items.</summary>
    Task<IReadOnlyList<PendingAsyncEvaluation>> DequeueAsync(int batchSize = 20, CancellationToken ct = default);

    Task MarkCompletedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default);
}
