using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Dto;

namespace Nexus.Fms.Infrastructure.Fraud;

/// <summary>
/// Real implementation of post-resolution side effects for confirmed-fraud cases:
///   1. Submit a SAR to NIBSS Fraud Bureau (FR-17, Workflow 5).
///   2. Add the sender to the internal blacklist (FR-28, R09).
///   3. Persist the SAR reference on the case for traceability.
/// </summary>
public sealed class CaseSideEffectsHandler : ICaseSideEffectsHandler
{
    private readonly INibssFraudBureauClient _nibss;
    private readonly IListRepository _lists;
    private readonly ICaseRepository _cases;
    private readonly ILogger<CaseSideEffectsHandler> _logger;

    public CaseSideEffectsHandler(
        INibssFraudBureauClient nibss,
        IListRepository lists,
        ICaseRepository cases,
        ILogger<CaseSideEffectsHandler> logger)
    {
        _nibss = nibss;
        _lists = lists;
        _cases = cases;
        _logger = logger;
    }

    public async Task OnConfirmedFraudAsync(FraudCase fraudCase, FraudAlert alert, CancellationToken ct = default)
    {
        // ── 1. Submit SAR (FR-17) ──────────────────────────────────────────────
        string sarRef;
        try
        {
            var payload = new SarPayload
            {
                Bvn           = alert.SenderBvn ?? string.Empty,
                AccountNumber = alert.SenderAccount,
                TransactionRef = alert.TransactionRef,
                FraudType     = "Confirmed Fraud - FMS Rule Engine",
                Amount        = alert.Amount,
                Date          = alert.CreatedAt,
                Narrative     = $"Case {fraudCase.CaseId} resolved as ConfirmedFraud. Risk score: {alert.CompositeRiskScore}."
            };
            sarRef = await _nibss.SubmitSarAsync(payload, ct);
            _logger.LogInformation(
                "SAR submitted for case {CaseId}; NIBSS reference {SarRef}",
                fraudCase.CaseId, sarRef);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SAR submission failed for case {CaseId}; continuing with auto-blacklist",
                fraudCase.CaseId);
            sarRef = $"FAILED-{Guid.NewGuid():N}"; // retain a trace even on failure
        }

        // ── 2. Persist SAR reference on case (FR-17) ──────────────────────────
        fraudCase.SarReference = sarRef;
        await _cases.SaveAsync(fraudCase, ct);

        // ── 3. Auto-blacklist sender (FR-28, R09 CannotBeOffset) ──────────────
        // Guard against duplicate entries (idempotent).
        var alreadyBlacklisted = await _lists.IsBlacklistedAsync(
            alert.SenderBvn, alert.SenderAccount, ct);
        if (!alreadyBlacklisted)
        {
            await _lists.AddAsync(new ListEntry
            {
                Bvn           = alert.SenderBvn,
                AccountNumber = alert.SenderAccount,
                ListType      = ListType.Blacklist,
                Source        = ListSource.Internal,
                Reason        = $"Auto-blacklisted: case {fraudCase.CaseId}, SAR {sarRef}",
                CreatedBy     = "system-fraud-engine"
            }, ct);
            _logger.LogInformation(
                "Account {Account} auto-blacklisted for case {CaseId}",
                alert.SenderAccount, fraudCase.CaseId);
        }
    }
}
