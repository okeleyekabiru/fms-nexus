namespace Nexus.Fms.Api.Security;

/// <summary>
/// Enforces API-key authentication on the screening endpoint.
/// The /api/screening path is called machine-to-machine by the NEXUS middleware, not by
/// human users, so it uses a shared secret rather than JWT (M6-1 plan note).
/// All other paths go through normal JWT bearer auth.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private const string ScreeningPrefix = "/api/screening";

    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        if (context.Request.Path.StartsWithSegments(ScreeningPrefix,
            StringComparison.OrdinalIgnoreCase))
        {
            var configuredKey = config["Screening:ApiKey"];

            // If no key is configured (e.g. dev), allow all requests through.
            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) ||
                    provided != configuredKey)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync(
                        "Missing or invalid API key. Provide X-Api-Key header.");
                    return;
                }
            }

            // Skip JWT auth for this path — it's already authenticated via API key.
            await _next(context);
            return;
        }

        await _next(context);
    }
}
