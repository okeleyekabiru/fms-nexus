using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Infrastructure.Persistence;

namespace Nexus.Fms.Infrastructure.Reporting;

/// <summary>
/// EF Core-backed implementation of <see cref="IReportingService"/> (FR-22–FR-24).
/// All queries are read-only (AsNoTracking). Heavy aggregations run in-DB where possible.
/// </summary>
public sealed class ReportingService : IReportingService
{
    private readonly FmsDbContext _db;

    public ReportingService(FmsDbContext db) => _db = db;

    public async Task<DashboardSummary> GetDashboardSummaryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Alert counts by verdict (single pass in DB)
        var alerts = await _db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .GroupBy(a => a.Verdict)
            .Select(g => new { Verdict = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Total(Verdict v) => alerts.FirstOrDefault(x => x.Verdict == v)?.Count ?? 0;
        var totalAlerts = alerts.Sum(x => x.Count);

        // Case counts by status and resolution
        var cases = await _db.Cases.AsNoTracking()
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to)
            .ToListAsync(ct);

        var dailySeries = await GetDailySeriesAsync(from, to, ct);

        return new DashboardSummary
        {
            From                    = from,
            To                      = to,
            TotalAlerts             = totalAlerts,
            BlockedTransactions     = Total(Verdict.Block),
            FlaggedTransactions     = Total(Verdict.Flag),
            MfaRequiredTransactions = Total(Verdict.RequireMfa),
            OpenCases               = cases.Count(c => c.Status is CaseStatus.New or CaseStatus.UnderInvestigation),
            EscalatedCases          = cases.Count(c => c.Status == CaseStatus.Escalated),
            ResolvedCasesTotal      = cases.Count(c => c.Status == CaseStatus.Resolved),
            ConfirmedFraudCases     = cases.Count(c => c.Resolution == CaseResolution.ConfirmedFraud),
            FalsePositiveCases      = cases.Count(c => c.Resolution == CaseResolution.FalsePositive),
            DailyAlertSeries        = dailySeries
        };
    }

    public async Task<IReadOnlyList<AlertHeatMapPoint>> GetAlertHeatMapAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Pull raw alert timestamps; bucket into hours in-process (EF can't easily group by DateTimeOffset hour).
        var rows = await _db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .Select(a => new { a.CreatedAt, a.Verdict })
            .ToListAsync(ct);

        var buckets = new Dictionary<DateTimeOffset, (int alerts, int blocks)>();

        // Pre-fill all hours in range with zeros.
        for (var h = from.ToUniversalTime().RoundToHour(); h <= to.ToUniversalTime(); h = h.AddHours(1))
            buckets[h] = (0, 0);

        foreach (var r in rows)
        {
            var hour = r.CreatedAt.ToUniversalTime().RoundToHour();
            if (!buckets.ContainsKey(hour)) continue;
            var (a, b) = buckets[hour];
            buckets[hour] = (a + 1, b + (r.Verdict == Verdict.Block ? 1 : 0));
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new AlertHeatMapPoint(kv.Key, kv.Value.alerts, kv.Value.blocks))
            .ToList();
    }

    public async Task<IReadOnlyList<AlertReportRow>> GetAlertReportAsync(
        DateTimeOffset from, DateTimeOffset to,
        RiskLevel? riskLevel, Verdict? verdict,
        int skip, int take, CancellationToken ct = default)
    {
        var q = _db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to);

        if (riskLevel.HasValue) q = q.Where(a => a.RiskLevel == riskLevel.Value);
        if (verdict.HasValue)   q = q.Where(a => a.Verdict == verdict.Value);

        var rows = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return rows.Select(a => new AlertReportRow
        {
            AlertId            = a.AlertId,
            TransactionRef     = a.TransactionRef,
            CustomerId         = a.CustomerId,
            CompositeRiskScore = a.CompositeRiskScore,
            RiskLevel          = a.RiskLevel.ToString(),
            Verdict            = a.Verdict.ToString(),
            ShadowOnly         = a.ShadowOnly,
            CreatedAt          = a.CreatedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<RuleEffectivenessRow>> GetRuleEffectivenessAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Pull triggered-rules JSON columns for the period; tally in-process.
        var jsons = await _db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .Select(a => a.TriggeredRulesJson)
            .ToListAsync(ct);

        var codeTally = new Dictionary<string, (string name, int count)>(StringComparer.Ordinal);

        foreach (var json in jsons)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var rules = JsonSerializer.Deserialize<List<TriggeredRuleJsonDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (rules is null) continue;
                foreach (var r in rules)
                {
                    if (!codeTally.TryGetValue(r.Code, out var entry))
                        entry = (r.Name, 0);
                    codeTally[r.Code] = (entry.name, entry.count + 1);
                }
            }
            catch (JsonException) { /* skip malformed */ }
        }

        var totalAlerts = jsons.Count;
        return codeTally
            .OrderByDescending(kv => kv.Value.count)
            .Select(kv => new RuleEffectivenessRow
            {
                RuleCode       = kv.Key,
                RuleName       = kv.Value.name,
                TriggerCount   = kv.Value.count,
                TriggerRatePct = totalAlerts == 0 ? 0 : Math.Round(kv.Value.count * 100.0 / totalAlerts, 2)
            })
            .ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<AlertHeatMapPoint>> GetDailySeriesAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var rows = await _db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .Select(a => new { a.CreatedAt, a.Verdict })
            .ToListAsync(ct);

        var days = new Dictionary<DateTimeOffset, (int a, int b)>();
        for (var d = from.ToUniversalTime().Date(); d <= to.ToUniversalTime(); d = d.AddDays(1))
            days[d] = (0, 0);

        foreach (var r in rows)
        {
            var day = r.CreatedAt.ToUniversalTime().Date();
            if (!days.ContainsKey(day)) continue;
            var (a, b) = days[day];
            days[day] = (a + 1, b + (r.Verdict == Verdict.Block ? 1 : 0));
        }

        return days.OrderBy(kv => kv.Key)
            .Select(kv => new AlertHeatMapPoint(kv.Key, kv.Value.a, kv.Value.b))
            .ToList();
    }

    private sealed record TriggeredRuleJsonDto(string Code, string Name, int Score, string Category);
}

/// <summary>DateTimeOffset rounding helpers.</summary>
internal static class DateTimeOffsetExtensions
{
    public static DateTimeOffset RoundToHour(this DateTimeOffset dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Offset);

    public static DateTimeOffset Date(this DateTimeOffset dt) =>
        new(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Offset);
}
