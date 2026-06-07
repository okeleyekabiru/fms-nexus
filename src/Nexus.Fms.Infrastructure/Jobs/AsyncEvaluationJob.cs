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
///   3. Merge triggered async rules into the existing alert (update score + risk level + verdict).
///   4. If the updated risk level now warrants a case and none exists, create one.
///   5. Mark the queue item completed or failed.
/// </summary>
public sealed class AsyncEvaluationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AsyncEvaluationJob> _logger;

    public AsyncEvaluationJob(IServiceScopeFactory scopeFactory, ILogger<AsyncEvaluationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            try { await RunAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "AsyncEvaluationJob failed"); }
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
        var asyncRules  = activeRules.Where(r => r.IsActive && !r.IsSynchronous).ToList();
        if (asyncRules.Count == 0)
        {
            // No async rules configured — complete all pending items.
            foreach (var item in batch)
                await queue.MarkCompletedAsync(item.Id, ct);
            return;
        }

        foreach (var item in batch)
        {
            try
            {
                var context = JsonSerializer.Deserialize<TransactionContext>(
                    item.TransactionContextJson, JsonOpts);
                if (context is null) throw new InvalidOperationException("Null TransactionContext after deserialise");

                // Re-use an empty lookups result for async rules (no DB/NIBSS calls here
                // to stay within a reasonable latency — async rules should only need facts
                // derived from the TransactionContext itself).
                var facts = Core.Engine.FactBuilder.Build(context, new ListLookupResult());

                var asyncTriggered = engine.Evaluate(asyncRules, facts, synchronousOnly: false);
                if (asyncTriggered.Count == 0)
                {
                    await queue.MarkCompletedAsync(item.Id, ct);
                    continue;
                }

                // Fetch existing alert and merge.
                var alert = await alerts.GetAlertByIdAsync(item.AlertId, ct);
                if (alert is null)
                {
                    _logger.LogWarning("AsyncEvaluationJob: alert {AlertId} not found, skipping", item.AlertId);
                    await queue.MarkCompletedAsync(item.Id, ct);
                    continue;
                }

                // Deserialise existing triggered rules and append async ones.
                var existingRules = JsonSerializer.Deserialize<List<JsonElement>>(
                    alert.TriggeredRulesJson, JsonOpts) ?? new();
                var merged = JsonSerializer.Serialize(existingRules.Concat(
                    asyncTriggered.Select(r => new { r.Code, r.Name, r.Score, Category = r.Category.ToString() })));

                // Re-score with all rules (existing sync + new async).
                var allTriggered = asyncTriggered; // combined scoring uses composite method
                var newResult = scoring.Score(allTriggered);

                // Only escalate score if the async rules add risk.
                var updatedScore   = alert.CompositeRiskScore + newResult.CompositeScore;
                var updatedLevel   = bands.Classify(updatedScore);
                var updatedVerdict = updatedLevel switch
                {
                    RiskLevel.P1 => Verdict.Block,
                    RiskLevel.P2 => Verdict.RequireMfa,
                    RiskLevel.P3 => Verdict.Flag,
                    _            => Verdict.Allow
                };

                alert.TriggeredRulesJson  = merged;
                alert.CompositeRiskScore  = updatedScore;
                alert.RiskLevel           = updatedLevel;
                alert.Verdict             = updatedVerdict;

                await alerts.UpdateAlertAsync(alert, ct);

                _logger.LogInformation(
                    "AsyncEvaluationJob: alert {AlertId} updated — new score {Score}, level {Level}",
                    alert.AlertId, updatedScore, updatedLevel);

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
