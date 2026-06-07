using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Microsoft.Extensions.Options;

namespace Nexus.Fms.Infrastructure.Jobs;

/// <summary>
/// Background job that auto-escalates stale cases (FR-21, Workflow 3 step 6).
///
/// Runs every 15 minutes:
///   • Cases with status New or UnderInvestigation that have not been touched
///     (or escalated) in the last 24 hours → status = Escalated.
///   • Cases still unresolved after 72 hours from creation → critical log entry
///     tagged HeadOfOpsAlert (to be wired to a real notification in M6).
/// </summary>
public sealed class CaseEscalationJob : BackgroundService
{
    private static readonly TimeSpan Interval         = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EscalateAfter    = TimeSpan.FromHours(24);
    private static readonly TimeSpan HeadOfOpsAfter   = TimeSpan.FromHours(72);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CaseEscalationJob> _logger;

    public CaseEscalationJob(IServiceScopeFactory scopeFactory, ILogger<CaseEscalationJob> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "CaseEscalationJob failed"); }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaseRepository>();
        var svc  = scope.ServiceProvider.GetRequiredService<ICaseManagementService>();

        var escalateThreshold = DateTimeOffset.UtcNow.Subtract(EscalateAfter);
        var stale = await repo.GetStaleAsync(escalateThreshold, ct);

        foreach (var c in stale)
        {
            await svc.EscalateAsync(c.CaseId, "system-escalation-job", ct);
            _logger.LogWarning(
                "Case {CaseId} auto-escalated after {Hours}h (FR-21)",
                c.CaseId, EscalateAfter.TotalHours);
        }

        // 72h Head-of-Ops alert (Workflow 3 step 6)
        var headOpsThreshold = DateTimeOffset.UtcNow.Subtract(HeadOfOpsAfter);
        var critical = await repo.ListAsync(
            CaseStatus.Escalated, assignedTo: null, skip: 0, take: 200, ct);

        var notifier = scope.ServiceProvider.GetRequiredService<IFraudNotificationService>();
        foreach (var c in critical.Where(c => c.CreatedAt < headOpsThreshold))
        {
            await notifier.NotifyHeadOfOpsAsync(
                c,
                $"Case unresolved for more than {HeadOfOpsAfter.TotalHours}h",
                ct);
        }
    }
}
