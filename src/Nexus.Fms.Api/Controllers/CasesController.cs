using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Contracts;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Fraud analyst case management endpoints (FR-19, FR-20, Workflow 3).
/// All routes require at least the fraud-analyst role.
/// </summary>
[ApiController]
[Route("api/cases")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
public sealed class CasesController : ControllerBase
{
    private readonly ICaseManagementService _svc;
    private readonly ICaseRepository        _repo;

    public CasesController(ICaseManagementService svc, ICaseRepository repo)
    {
        _svc  = svc;
        _repo = repo;
    }

    // ── Queries ────────────────────────────────────────────────────────────────

    /// <summary>List cases, optionally filtered by status or assigned analyst.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<FraudCaseDto>>> List(
        [FromQuery] CaseStatus? status,
        [FromQuery] string? assignedTo,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _repo.ListAsync(status, assignedTo, skip, take, ct);
        return Ok(new PagedResult<FraudCaseDto>(
            items.Select(c => FraudCaseDto.From(c)).ToList(), skip, take));
    }

    /// <summary>Full case detail, including the triggering alert summary.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FraudCaseDto>> GetById(Guid id, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        if (c is null) return NotFound();
        var alert = await _repo.GetAlertByIdAsync(c.AlertId, ct);
        return Ok(FraudCaseDto.From(c, alert));
    }

    // ── Alert direct lookup ────────────────────────────────────────────────────

    /// <summary>Alert detail (FR-18).</summary>
    [HttpGet("/api/alerts/{id:guid}")]
    public async Task<ActionResult<FraudAlertSummaryDto>> GetAlert(Guid id, CancellationToken ct)
    {
        var a = await _repo.GetAlertByIdAsync(id, ct);
        if (a is null) return NotFound();
        return Ok(FraudAlertSummaryDto.From(a));
    }

    // ── Mutations ──────────────────────────────────────────────────────────────

    /// <summary>Assign the case to an analyst (FR-20).</summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<FraudCaseDto>> Assign(
        Guid id, [FromBody] AssignCaseRequest req, CancellationToken ct)
    {
        try { return Ok(FraudCaseDto.From(await _svc.AssignAsync(id, req.AnalystId, ct))); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Append an investigation note to the case (FR-20).</summary>
    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<FraudCaseDto>> AddNote(
        Guid id, [FromBody] AddNoteRequest req, CancellationToken ct)
    {
        var analystId = User.Identity?.Name ?? "unknown";
        try { return Ok(FraudCaseDto.From(await _svc.AddNoteAsync(id, req.Text, analystId, ct))); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Escalate the case to a supervisor (FR-20).</summary>
    [HttpPost("{id:guid}/escalate")]
    public async Task<ActionResult<FraudCaseDto>> Escalate(Guid id, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "unknown";
        try { return Ok(FraudCaseDto.From(await _svc.EscalateAsync(id, userId, ct))); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Resolve the case with a verdict (FR-20). Confirmed fraud fires SAR (FR-17).</summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<FraudCaseDto>> Resolve(
        Guid id, [FromBody] ResolveCaseRequest req, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "unknown";
        try
        {
            var c = await _svc.ResolveAsync(id, req.Resolution, userId, ct);
            if (req.VerdictOverride.HasValue)
                c = await _svc.OverrideVerdictAsync(id, req.VerdictOverride.Value, userId, ct);
            return Ok(FraudCaseDto.From(c));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
