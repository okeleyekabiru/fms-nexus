using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Fraud;

/// <summary>
/// Placeholder until M2 (CaseSideEffectsHandler) is wired up.
/// Logs the intent but does not actually submit a SAR or add to the blacklist.
/// </summary>
public sealed class StubCaseSideEffectsHandler : ICaseSideEffectsHandler
{
    private readonly ILogger<StubCaseSideEffectsHandler> _logger;
    public StubCaseSideEffectsHandler(ILogger<StubCaseSideEffectsHandler> logger) => _logger = logger;

    public Task OnConfirmedFraudAsync(FraudCase fraudCase, FraudAlert alert, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "StubCaseSideEffectsHandler: ConfirmedFraud for case {CaseId} — " +
            "SAR submission and auto-blacklist not yet implemented (M2).",
            fraudCase.CaseId);
        return Task.CompletedTask;
    }
}
