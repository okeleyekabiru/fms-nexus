using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>
/// Appends immutable audit records for all admin mutations (FR-26).
/// Implementations must never modify or delete existing entries.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string entityType,
        Guid? entityId,
        string performedBy,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetEntriesAsync(
        string? entityType,
        Guid? entityId,
        int skip,
        int take,
        CancellationToken ct = default);
}
