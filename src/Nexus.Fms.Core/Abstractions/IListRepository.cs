using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Internal blacklist/whitelist lookups and management (FR-28).</summary>
public interface IListRepository
{
    Task<bool> IsWhitelistedAsync(string? bvn, string? account, CancellationToken ct = default);
    Task<bool> IsBlacklistedAsync(string? bvn, string? account, CancellationToken ct = default);
    Task AddAsync(ListEntry entry, CancellationToken ct = default);

    /// <summary>Paginated query for admin UI (FR-28).</summary>
    Task<IReadOnlyList<ListEntry>> GetEntriesAsync(
        ListType? listType, int skip, int take, CancellationToken ct = default);

    Task<ListEntry?> GetByIdAsync(Guid entryId, CancellationToken ct = default);

    Task RemoveAsync(Guid entryId, CancellationToken ct = default);
}
