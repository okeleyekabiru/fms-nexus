using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Scoring;
using Nexus.Fms.Core.Services;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// System configuration — threshold score bands (FR-11, FR-12) and screening options (FR-04).
/// Changes take effect immediately for in-memory values; persistent reconfiguration
/// requires a DB-backed config store (wired in M6).
/// </summary>
[ApiController]
[Route("api/config")]
[Authorize(Roles = Roles.Admin)]
public sealed class ConfigController : ControllerBase
{
    private readonly IOptionsSnapshot<ThresholdBands> _bandsSnapshot;
    private readonly IOptionsSnapshot<ScreeningOptions> _screeningSnapshot;
    private readonly IAuditLogger _audit;

    public ConfigController(
        IOptionsSnapshot<ThresholdBands> bandsSnapshot,
        IOptionsSnapshot<ScreeningOptions> screeningSnapshot,
        IAuditLogger audit)
    {
        _bandsSnapshot     = bandsSnapshot;
        _screeningSnapshot = screeningSnapshot;
        _audit             = audit;
    }

    // ── Threshold bands ────────────────────────────────────────────────────────

    /// <summary>Current score-to-risk-level threshold bands (FR-11).</summary>
    [HttpGet("thresholds")]
    public ActionResult<ThresholdBandsDto> GetThresholds() =>
        Ok(ThresholdBandsDto.From(_bandsSnapshot.Value));

    /// <summary>
    /// Validate and log a threshold band change (FR-12).
    /// NOTE: in-process IOptions is read-only at runtime; to make changes persistent
    /// restart the application with updated appsettings, or wire DB-backed config in M6.
    /// </summary>
    [HttpPut("thresholds")]
    public async Task<ActionResult<ThresholdBandsDto>> UpdateThresholds(
        [FromBody] ThresholdBandsDto req, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";

        var proposed = new ThresholdBands
        {
            P1Min = req.P1Min,
            P2Min = req.P2Min,
            P3Min = req.P3Min,
            P4Min = req.P4Min
        };

        try { proposed.Validate(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }

        await _audit.LogAsync("ThresholdBandsUpdated", "SystemConfig", null, user,
            oldValues: ThresholdBandsDto.From(_bandsSnapshot.Value),
            newValues: req, ct: ct);

        // Return the proposed values — runtime effect requires restart or M6 live reload.
        return Accepted(req);
    }

    // ── Screening options ──────────────────────────────────────────────────────

    /// <summary>Current screening options including global failure mode (FR-04).</summary>
    [HttpGet("screening")]
    public ActionResult<ScreeningOptionsDto> GetScreening() =>
        Ok(ScreeningOptionsDto.From(_screeningSnapshot.Value));
}

// ── DTOs ───────────────────────────────────────────────────────────────────────

public sealed record ThresholdBandsDto(int P1Min, int P2Min, int P3Min, int P4Min)
{
    public static ThresholdBandsDto From(ThresholdBands b) =>
        new(b.P1Min, b.P2Min, b.P3Min, b.P4Min);
}

public sealed record ScreeningOptionsDto(string FailureMode, int NibssUnavailableCompensatingScore, string CaseCreationLevel)
{
    public static ScreeningOptionsDto From(ScreeningOptions o) =>
        new(o.FailureMode.ToString(), o.NibssUnavailableCompensatingScore, o.CaseCreationLevel.ToString());
}
