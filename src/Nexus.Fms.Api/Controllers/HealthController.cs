using Microsoft.AspNetCore.Mvc;

namespace Nexus.Fms.Api.Controllers;

/// <summary>Liveness probe for the NEXUS SLA / 99.5% availability monitoring (NFR-03).</summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy", service = "Nexus.Fms", utc = DateTimeOffset.UtcNow });
}
