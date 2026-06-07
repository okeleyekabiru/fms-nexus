using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Provides the active rule set to the engine. Backed by the database (FR-08, NFR-07).</summary>
public interface IRuleRepository
{
    Task<IReadOnlyList<FraudRule>> GetActiveRulesAsync(CancellationToken ct = default);
}

/// <summary>Result of a NIBSS Fraud Bureau watchlist lookup (FR-14–FR-16).</summary>
public sealed record NibssLookupResult
{
    public bool OnWatchlist { get; init; }
    public bool ConfirmedFraud { get; init; }
    /// <summary>True when the lookup could not be completed (timeout/unavailable) — FR-16.</summary>
    public bool Unavailable { get; init; }
    public string? RawResponse { get; init; }

    public static NibssLookupResult NotFound { get; } = new();
    public static NibssLookupResult Failed { get; } = new() { Unavailable = true };
}
