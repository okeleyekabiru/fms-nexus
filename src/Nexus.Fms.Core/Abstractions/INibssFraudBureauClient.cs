using Nexus.Fms.Core.Dto;

namespace Nexus.Fms.Core.Abstractions;

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
