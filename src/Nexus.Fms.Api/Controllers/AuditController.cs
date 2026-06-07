using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Contracts;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Audit trail query endpoint (FR-26).
/// All admin roles can read the audit log; only admins and approvers typically need it.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditLogger _audit;

    public AuditController(IAuditLogger audit) => _audit = audit;

    /// <summary>
    /// Paginated audit log, optionally filtered by entity type and/or entity id (FR-26).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> GetEntries(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _audit.GetEntriesAsync(entityType, entityId, skip, take, ct);
        return Ok(new PagedResult<AuditLogEntryDto>(
            items.Select(AuditLogEntryDto.From).ToList(), skip, take));
    }
}
