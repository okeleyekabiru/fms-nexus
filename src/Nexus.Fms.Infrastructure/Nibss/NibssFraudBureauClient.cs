using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;

namespace Nexus.Fms.Infrastructure.Nibss;

/// <summary>
/// HTTP client for the NIBSS Fraud Bureau API (FR-14–FR-17). Resilience (circuit breaker,
/// 3 retries, 5s timeout — NFR-08) is configured via Polly in DI (see ServiceCollectionExtensions).
/// Any failure surfaces as <see cref="NibssLookupResult.Failed"/> so screening never blocks on NIBSS (FR-16).
/// </summary>
public sealed class NibssFraudBureauClient : INibssFraudBureauClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NibssFraudBureauClient> _logger;

    public NibssFraudBureauClient(HttpClient http, ILogger<NibssFraudBureauClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<NibssLookupResult> LookupAsync(string? bvn, string? account, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bvn) && string.IsNullOrWhiteSpace(account))
            return NibssLookupResult.NotFound;

        try
        {
            using var resp = await _http.PostAsJsonAsync("watchlist/lookup", new { bvn, account }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("NIBSS lookup returned {Status}", resp.StatusCode);
                return NibssLookupResult.Failed;
            }

            var dto = await resp.Content.ReadFromJsonAsync<NibssLookupDto>(cancellationToken: ct);
            if (dto is null) return NibssLookupResult.Failed;

            return new NibssLookupResult
            {
                OnWatchlist = dto.OnWatchlist,
                ConfirmedFraud = dto.ConfirmedFraud,
                RawResponse = dto.ToString()
            };
        }
        catch (Exception ex)
        {
            // Includes Polly BrokenCircuitException / TimeoutRejectedException after retries are exhausted.
            _logger.LogWarning(ex, "NIBSS Fraud Bureau unavailable; applying compensating score (FR-16)");
            return NibssLookupResult.Failed;
        }
    }

    public async Task<string> SubmitSarAsync(SarPayload payload, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("sar/submit", payload, ct);
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<SarResponseDto>(cancellationToken: ct);
        return dto?.Reference ?? throw new InvalidOperationException("NIBSS SAR response missing reference");
    }

    private sealed record NibssLookupDto(bool OnWatchlist, bool ConfirmedFraud);
    private sealed record SarResponseDto(string Reference);
}

/// <summary>
/// Offline stub used in Development / when NIBSS sandbox access is not yet provisioned (Dependencies §7a).
/// Treats a small set of seeded BVNs as watchlisted so the pipeline can be exercised end-to-end.
/// </summary>
public sealed class StubNibssFraudBureauClient : INibssFraudBureauClient
{
    private static readonly HashSet<string> Watchlisted = new(StringComparer.OrdinalIgnoreCase)
    { "22222222222", "33333333333" };
    private static readonly HashSet<string> ConfirmedFraud = new(StringComparer.OrdinalIgnoreCase)
    { "99999999999" };

    public Task<NibssLookupResult> LookupAsync(string? bvn, string? account, CancellationToken ct = default)
    {
        var key = bvn ?? account ?? string.Empty;
        if (ConfirmedFraud.Contains(key))
            return Task.FromResult(new NibssLookupResult { OnWatchlist = true, ConfirmedFraud = true, RawResponse = "stub:confirmed" });
        if (Watchlisted.Contains(key))
            return Task.FromResult(new NibssLookupResult { OnWatchlist = true, RawResponse = "stub:watchlist" });
        return Task.FromResult(NibssLookupResult.NotFound);
    }

    public Task<string> SubmitSarAsync(SarPayload payload, CancellationToken ct = default) =>
        Task.FromResult($"STUB-SAR-{Guid.NewGuid():N}");
}
