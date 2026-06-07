using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Real-time fraud dashboard data (FR-22, FR-23).
/// Requires at least the analyst role; no write operations.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
public sealed class DashboardController : ControllerBase
{
    private readonly IReportingService _reporting;

    public DashboardController(IReportingService reporting) => _reporting = reporting;

    /// <summary>
    /// Aggregate summary for the main dashboard (FR-22).
    /// Defaults to the last 7 days if no range is provided.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var end   = to   ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-7);
        return Ok(await _reporting.GetDashboardSummaryAsync(start, end, ct));
    }

    /// <summary>
    /// Alert heat-map data — alert and block counts per hour (FR-23).
    /// Defaults to the last 7 days.
    /// </summary>
    [HttpGet("heatmap")]
    public async Task<ActionResult<IReadOnlyList<AlertHeatMapPoint>>> GetHeatMap(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var end   = to   ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-7);
        return Ok(await _reporting.GetAlertHeatMapAsync(start, end, ct));
    }

    /// <summary>Rule effectiveness metrics for the current period (FR-22).</summary>
    [HttpGet("rule-effectiveness")]
    public async Task<ActionResult<IReadOnlyList<RuleEffectivenessRow>>> GetRuleEffectiveness(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var end   = to   ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);
        return Ok(await _reporting.GetRuleEffectivenessAsync(start, end, ct));
    }
}
