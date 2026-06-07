using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Contracts;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Paginated alert report — replaces the FR-20 "30-day history" deep-link requirement
/// with a native FMS query endpoint (FR-20 gap, FR-24).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportingService _reporting;

    public ReportsController(IReportingService reporting) => _reporting = reporting;

    /// <summary>
    /// Paginated alert report with optional risk-level and verdict filters (FR-24).
    /// Defaults to the last 30 days.
    /// </summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<PagedResult<AlertReportRow>>> GetAlerts(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] RiskLevel? riskLevel,
        [FromQuery] Verdict? verdict,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var end   = to   ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        var items = await _reporting.GetAlertReportAsync(start, end, riskLevel, verdict, skip, take, ct);
        return Ok(new PagedResult<AlertReportRow>(items, skip, take));
    }
}
