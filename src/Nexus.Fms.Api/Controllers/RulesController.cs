using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Contracts;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Fraud rule administration — CRUD, mode control, and maker-checker workflow (FR-05–FR-08, FR-25, FR-26).
///
/// Workflow:
///   fraud-admin creates a rule  → ApprovalStatus = PendingApproval, Mode = Disabled
///   fraud-approver approves     → ApprovalStatus = Approved  (admin then sets Mode)
///   fraud-approver rejects      → ApprovalStatus = Rejected, Mode = Disabled
/// </summary>
[ApiController]
[Route("api/rules")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin},{Roles.Approver}")]
public sealed class RulesController : ControllerBase
{
    private readonly IRuleRepository _rules;
    private readonly IAuditLogger    _audit;

    public RulesController(IRuleRepository rules, IAuditLogger audit)
    {
        _rules = rules;
        _audit = audit;
    }

    // ── Queries ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FraudRuleDto>>> GetAll(
        [FromQuery] RuleApprovalStatus? approvalStatus,
        CancellationToken ct)
    {
        var rules = await _rules.GetAllAsync(approvalStatus, ct);
        return Ok(rules.Select(FraudRuleDto.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FraudRuleDto>> GetById(Guid id, CancellationToken ct)
    {
        var rule = await _rules.GetByIdAsync(id, ct);
        return rule is null ? NotFound() : Ok(FraudRuleDto.From(rule));
    }

    // ── Mutations (admin only) ─────────────────────────────────────────────────

    /// <summary>Create a new rule. Requires approval before it can be activated (FR-25).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<FraudRuleDto>> Create(
        [FromBody] CreateRuleRequest req,
        CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        var rule = new FraudRule
        {
            Code           = req.Code,
            Name           = req.Name,
            Description    = req.Description,
            Category       = req.Category,
            ConditionsJson = req.ConditionsJson,
            Score          = req.Score,
            IsSynchronous  = req.IsSynchronous,
            CannotBeOffset = req.CannotBeOffset,
            Mode           = RuleMode.Disabled,           // must be approved before activation
            ApprovalStatus = RuleApprovalStatus.PendingApproval,
            CreatedBy      = user
        };

        var saved = await _rules.AddAsync(rule, ct);
        await _audit.LogAsync("RuleCreated", "FraudRule", saved.RuleId, user,
            newValues: FraudRuleDto.From(saved), ct: ct);

        return CreatedAtAction(nameof(GetById), new { id = saved.RuleId }, FraudRuleDto.From(saved));
    }

    /// <summary>Update rule definition. Resets approval to PendingApproval (FR-25).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<FraudRuleDto>> Update(
        Guid id, [FromBody] UpdateRuleRequest req, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        var existing = await _rules.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        var before = FraudRuleDto.From(existing);

        existing.Name           = req.Name;
        existing.Description    = req.Description;
        existing.Category       = req.Category;
        existing.ConditionsJson = req.ConditionsJson;
        existing.Score          = req.Score;
        existing.Mode           = req.Mode;
        existing.IsSynchronous  = req.IsSynchronous;
        existing.CannotBeOffset = req.CannotBeOffset;
        existing.ApprovalStatus = RuleApprovalStatus.PendingApproval; // re-approval required

        var updated = await _rules.UpdateAsync(existing, ct);
        await _audit.LogAsync("RuleUpdated", "FraudRule", id, user,
            oldValues: before, newValues: FraudRuleDto.From(updated), ct: ct);

        return Ok(FraudRuleDto.From(updated));
    }

    /// <summary>Disable (soft-delete) a rule.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        var existing = await _rules.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        await _rules.DisableAsync(id, ct);
        await _audit.LogAsync("RuleDisabled", "FraudRule", id, user,
            oldValues: FraudRuleDto.From(existing), ct: ct);

        return NoContent();
    }

    // ── Maker-checker (approver role) ──────────────────────────────────────────

    /// <summary>Approve a pending rule change (FR-25). The rule remains Disabled until the
    /// admin explicitly sets its mode.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.Approver)]
    public async Task<ActionResult<FraudRuleDto>> Approve(Guid id, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        try
        {
            var rule = await _rules.ApproveAsync(id, user, ct);
            await _audit.LogAsync("RuleApproved", "FraudRule", id, user,
                newValues: FraudRuleDto.From(rule), ct: ct);
            return Ok(FraudRuleDto.From(rule));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Reject a pending rule change (FR-25).</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.Approver)]
    public async Task<ActionResult<FraudRuleDto>> Reject(
        Guid id, [FromBody] RejectRuleRequest req, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        try
        {
            var rule = await _rules.RejectAsync(id, user, req.Reason, ct);
            await _audit.LogAsync("RuleRejected", "FraudRule", id, user,
                newValues: new { req.Reason }, ct: ct);
            return Ok(FraudRuleDto.From(rule));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Set a rule's deployment mode (Disabled / Shadow / Live). Requires prior approval (FR-07).</summary>
    [HttpPost("{id:guid}/mode")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<FraudRuleDto>> SetMode(
        Guid id, [FromBody] SetRuleModeRequest req, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        var existing = await _rules.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        if (req.Mode != RuleMode.Disabled &&
            existing.ApprovalStatus != RuleApprovalStatus.Approved)
            return BadRequest("Rule must be approved before it can be activated.");

        existing.Mode = req.Mode;
        var updated = await _rules.UpdateAsync(existing, ct);
        await _audit.LogAsync("RuleModeChanged", "FraudRule", id, user,
            newValues: new { Mode = req.Mode.ToString() }, ct: ct);

        return Ok(FraudRuleDto.From(updated));
    }
}
