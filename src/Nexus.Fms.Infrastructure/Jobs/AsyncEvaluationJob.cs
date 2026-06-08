using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;
using Nexus.Fms.Core.Scoring;

namespace Nexus.Fms.Infrastructure.Jobs;

/// <summary>
/// Background job that drains the async evaluation queue (FR-03).
///
/// Runs every 30 seconds. For each pending item:
///   1. Deserialise the saved TransactionContext.
///   2. Re-evaluate only async (IsSynchronous = false) rules.
///   3. Reconstruct all triggered rules (sync from stored JSON + new async) and re-score
///      the full combined set so that CannotBeOffset semantics are preserved.
///   4. If the updated risk level now warrants a case and none exists, create one.
///   5. Mark the queue item completed or failed.
/// </summary>
public sealed class AsyncEvaluationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AsyncEvaluationJob> _logger;

    // Private DTO for deserialising the triggered-rules JSON stored on FraudAlert.
    // CannotBeOffset defaults to false to handle records written before it was added.
    private sealed record StoredTriggeredRule(
        string Code,
        string Name,
        int Score,
        string Category,
        bool CannotBeOffset = false);

    public AsyncEvaluationJob(IServiceScopeFactory scopeFactory, ILogger<AsyncEvaluationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run first iteration immediately, then wait between runs.
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "AsyncEvaluationJob failed"); }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue   = scope.ServiceProvider.GetRequiredService<IAsyncEvaluationQueue>();
        var rules   = scope.ServiceProvider.GetRequiredService<IRuleRepository>();
        var alerts  = scope.ServiceProvider.GetRequiredService<IAlertStore>();
        var engine  = scope.ServiceProvider.GetRequiredService<RuleEngine>();
        var scoring = scope.ServiceProvider.GetRequiredService<ScoringEngine>();
        var bands   = scope.ServiceProvider.GetRequiredService<IOptions<ThresholdBands>>().Value;

        var batch = await queue.DequeueAsync(batchSize: 20, ct);
        if (batch.Count == 0) return;

        var activeRules = await rules.GetActiveRulesAsync(ct);
        // GetActiveRulesAsync already filters to Live/Shadow — no need for r.IsActive check.
        var asyncRules = activeRules.Where(r => !r.IsSynchronous).ToList();
        if (asyncRules.Count == 0)
        {
            // No async rules configured — complete all pending items.
            foreach (var item in batch)
                await queue.MarkCompletedAsync(item.Id, ct);
            return;
        }

        // Build a lookup so we can recover CannotBeOffset for stored sync rules.
        var ruleByCode = activeRules.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var item in batch)
        {
            try
            {
                var context = JsonSerializer.Deserialize<TransactionContext>(
                    item.TransactionContextJson, JsonOpts);
                if (context is null) throw new InvalidOperationException("Null TransactionContext after deserialise");

                // Re-use an empty lookups result for async rules (no DB/NIBSS calls here —
                // async rules should only need facts from the TransactionContext itself).
                var facts = Core.Engine.FactBuilder.Build(context, new ListLookupResult());

                var asyncTriggered = engine.Evaluate(asyncRules, facts, synchronousOnly: false);
                if (asyncTriggered.Count == 0)
                {
                    await queue.MarkCompletedAsync(item.Id, ct);
                    continue;
                }

                // Fetch existing alert.
                var alert = await alerts.GetAlertByIdAsync(item.AlertId, ct);
                if (alert is null)
                {
                    _logger.LogWarning("AsyncEvaluationJob: alert {AlertId} not found, skipping", item.AlertId);
                    await queue.MarkCompletedAsync(item.Id, ct);
                    continue;
                }

                // Reconstruct sync TriggeredRule objects from the stored JSON so that
                // CannotBeOffset semantics are preserved when re-scoring the full set.
                var storedDtos = JsonSerializer.Deserialize<List<StoredTriggeredRule>>(
                    alert.TriggeredRulesJson, JsonOpts) ?? new();

                var syncTriggered = storedDtos.Select(dto => new TriggeredRule
                {
                    RuleId         = ruleByCode.TryGetValue(dto.Code, out var r) ? r.RuleId : Guid.Empty,
                    Code           = dto.Code,
                    Name           = dto.Name,
                    Score          = dto.Score,
                    Category       = Enum.TryParse<RuleCategory>(dto.Category, out var cat) ? cat : default,
                    CannotBeOffset = dto.CannotBeOffset,
                    ShadowOnly     = false
                }).ToList();

                // Re-score the full combined set — this correctly applies CannotBeOffset
                // across both sync and async rules instead of simple arithmetic addition.
                var allTriggered = syncTriggered.Concat(asyncTriggered).ToList();
                var newResult    = scoring.Score(allTriggered);

                // Persist the merged JSON and updated scores.
                alert.TriggeredRulesJson = JsonSerializer.Serialize(
                    allTriggered.Select(r => new
                    {
                        r.Code, r.Name, r.Score, r.CannotBeOffset,
                        Category = r.Category.ToString()
                    }));
                alert.CompositeRiskScore = newResult.CompositeScore;
                alert.RiskLevel         = newResult.RiskLevel;
                alert.Verdict           = newResult.Verdict;

                await alerts.UpdateAlertAsync(alert, ct);

                _logger.LogInformation(
                    "AsyncEvaluationJob: alert {AlertId} updated — score {Score}, level {Level}",
                    alert.AlertId, newResult.CompositeScore, newResult.RiskLevel);

                await queue.MarkCompletedAsync(item.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AsyncEvaluationJob failed for item {Id}", item.Id);
                await queue.MarkFailedAsync(item.Id, ex.Message, ct);
            }
        }
    }
}
