using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Internal blacklist/whitelist lookups (FR-28).</summary>
public interface IListRepository
{
    Task<bool> IsWhitelistedAsync(string? bvn, string? account, CancellationToken ct = default);
    Task<bool> IsBlacklistedAsync(string? bvn, string? account, CancellationToken ct = default);
    Task AddAsync(ListEntry entry, CancellationToken ct = default);
}
