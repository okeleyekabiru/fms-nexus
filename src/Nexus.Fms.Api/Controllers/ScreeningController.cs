using Microsoft.AspNetCore.Mvc;
using Nexus.Fms.Api.Contracts;
using Nexus.Fms.Core.Abstractions;

namespace Nexus.Fms.Api.Controllers;

/// <summary>
/// Real-time transaction screening endpoint invoked by the NEXUS middleware before a
/// transaction reaches core banking (FR-01, Workflow 1).
/// </summary>
[ApiController]
[Route("api/screening")]
public sealed class ScreeningController : ControllerBase
{
    private readonly IScreeningService _screening;

    public ScreeningController(IScreeningService screening) => _screening = screening;

    /// <summary>
    /// Evaluate a transaction and return a verdict (ALLOW / FLAG / REQUIRE_MFA / BLOCK).
    /// Target latency &lt; 50ms (NFR-01).
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ScreeningResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScreeningResponse>> Evaluate(
        [FromBody] ScreenTransactionRequest request, CancellationToken ct)
    {
        var response = await _screening.ScreenAsync(request.ToDomain(), ct);
        return Ok(response);
    }
}
