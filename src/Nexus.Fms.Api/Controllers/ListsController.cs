using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Contracts;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Blacklist / whitelist management endpoints (FR-28).
/// Requires fraud-admin role for mutations; analysts may query.
/// </summary>
[ApiController]
[Route("api/lists")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
public sealed class ListsController : ControllerBase
{
    private readonly IListRepository _lists;

    public ListsController(IListRepository lists) => _lists = lists;

    /// <summary>Paginated list of blacklist/whitelist entries.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ListEntryDto>>> GetEntries(
        [FromQuery] ListType? listType,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _lists.GetEntriesAsync(listType, skip, take, ct);
        return Ok(new PagedResult<ListEntryDto>(
            items.Select(ListEntryDto.From).ToList(), skip, take));
    }

    /// <summary>Single entry detail.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListEntryDto>> GetById(Guid id, CancellationToken ct)
    {
        var e = await _lists.GetByIdAsync(id, ct);
        return e is null ? NotFound() : Ok(ListEntryDto.From(e));
    }

    /// <summary>Manually add a BVN/account to the blacklist or whitelist (FR-28).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ListEntryDto>> Add(
        [FromBody] AddListEntryRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Bvn) && string.IsNullOrWhiteSpace(req.AccountNumber))
            return BadRequest("At least one of Bvn or AccountNumber must be provided.");

        var entry = new ListEntry
        {
            Bvn           = req.Bvn,
            AccountNumber = req.AccountNumber,
            ListType      = req.ListType,
            Source        = ListSource.Internal,
            Reason        = req.Reason,
            CreatedBy     = User.Identity?.Name ?? "unknown"
        };

        await _lists.AddAsync(entry, ct);
        return CreatedAtAction(nameof(GetById), new { id = entry.EntryId }, ListEntryDto.From(entry));
    }

    /// <summary>Remove an entry from the list (FR-28).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var existing = await _lists.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        await _lists.RemoveAsync(id, ct);
        return NoContent();
    }
}
