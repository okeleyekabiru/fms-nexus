using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Infrastructure.Persistence;

namespace Nexus.Fms.Infrastructure.Jobs;

/// <summary>
/// Nightly data-retention sweep (NFR-06).
///
/// Deletes alerts and completed async-evaluation items older than the configured
/// retention period (default 365 days). Audit logs and resolved cases are
/// retained indefinitely for regulatory compliance.
///
/// Runs once per day at startup + every 24 hours thereafter.
/// </summary>
public sealed class RetentionJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionJob> _logger;
    private readonly int _retentionDays;

    public RetentionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<RetentionJob> logger,
        int retentionDays = 365)
    {
        _scopeFactory  = scopeFactory;
        _logger        = logger;
        _retentionDays = retentionDays;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup, then every 24h.
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "RetentionJob failed"); }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_retentionDays);
        _logger.LogInformation("RetentionJob: purging records older than {Cutoff:O}", cutoff);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FmsDbContext>();

        // Purge shadow-only alerts with no associated case (low-risk noise).
        var deletedShadowAlerts = await db.Alerts
            .Where(a => a.CreatedAt < cutoff && a.ShadowOnly)
            .ExecuteDeleteAsync(ct);

        // Purge non-shadow alerts older than retention with no open case.
        // Resolved/closed cases are fine to purge; open cases retain their parent alert.
        var deletedLiveAlerts = await db.Alerts
            .Where(a => a.CreatedAt < cutoff
                        && !a.ShadowOnly
                        && !