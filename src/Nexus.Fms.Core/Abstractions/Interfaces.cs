using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Provides the active rule set to the engine. Backed by the database (FR-08, NFR-07).</summary>
public interface IRuleRepository
{
    Task<IReadOnlyList<FraudRule>> GetActiveRulesAsync(CancellationToken ct = default);
}

/// <summary>Internal blacklist/whitelist lookups (FR-28).</summary>
public interface IListRepository
{
    Task<bool> IsWhitelistedAsync(string? bvn, string? account, CancellationToken ct = default);
    Task<bool> IsBlacklistedAsync(string? bvn, string? account, CancellationToken ct = default);
    Task AddAsync(ListEntry entry, CancellationToken ct = default);
}

/// <summary>Persists alerts and cases (FR-18, FR-19).</summary>
public interface IAlertStore
{
    Task<FraudAlert> SaveAlertAsync(FraudAlert alert, CancellationToken ct = default);
    Task<FraudCase> CreateCaseAsync(FraudCase fraudCase, CancellationToken ct = default);
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

/// <summary>
/// Client for the NIBSS Fraud Bureau API (FR-14–FR-17). Implementations must be resilient:
/// circuit breaker with 3 retries and a 5s timeout, never throwing to the caller (NFR-08).
/// </summary>
public interface INibssFraudBureauClient
{
    Task<NibssLookupResult> LookupAsync(string? bvn, string? account, CancellationToken ct = default);

    /// <summary>Submit a Suspicious Activity Report; returns the NIBSS reference (FR-17, Workflow 5).</summary>
    Task<string> SubmitSarAsync(SarPayload payload, CancellationToken ct = default);
}

/// <summary>SAR payload submitted to NIBSS when fraud is confirmed (Workflow 5).</summary>
public sealed record SarPayload
{
    public required string Bvn { get; init; }
    public required string AccountNumber { get; init; }
    public required string TransactionRef { get; init; }
    public required string FraudType { get; init; }
    public required decimal Amount { get; init; }
    public required DateTimeOffset Date { get; init; }
    public string? Narrative { get; init; }
}

/// <summary>The screening entry point invoked by the NEXUS middleware (Workflow 1, step 3-5).</summary>
public interface IScreeningService
{
    Task<ScreeningResponse> ScreenAsync(TransactionContext context, CancellationToken ct = default);
}

/// <summary>The verdict envelope returned to the middleware (Workflow 1, step 5).</summary>
public sealed record ScreeningResponse
{
    public required string TransactionRef { get; init; }
    public required Verdict Verdict { get; init; }
    public required int RiskScore { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public required IReadOnlyList<TriggeredRuleDto> TriggeredRules { get; init; }
    public Guid? AlertId { get; init; }
    public Guid? CaseId { get; init; }

    /// <summary>True when the verdict was produced by the fail-open/fail-closed fallback (FR-04).</summary>
    public bool Bypassed { get; init; }
    public long EvaluationMs { get; init; }
}

public sealed record TriggeredRuleDto(string Code, string Name, int Score, string Category);
