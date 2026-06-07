using Microsoft.Extensions.Logging;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Services;

/// <summary>
/// Implements the fraud analyst case workflow (FR-19, FR-20, FR-21, Workflow 3).
/// </summary>
public sealed class CaseManagementService : ICaseManagementService
{
    private readonly ICaseRepository _cases;
    private readonly ICaseSideEffectsHandler _sideEffects;
    private readonly ILogger<CaseManagementService> _logger;

    public CaseManagementService(
        ICaseRepository cases,
        ICaseSideEffectsHandler sideEffects,
        ILogger<CaseManagementService> logger)
    {
        _cases      = cases;
        _sideEffects = sideEffects;
        _logger     = logger;
    }

    public async Task<FraudCase> AssignAsync(Guid caseId, string analystId, CancellationToken ct = default)
    {
        var fraudCase = await GetFraudAsync(caseId, ct);
        fraudCase.AssignedTo = analystId;
        if (fraudCase.Status == CaseStatus.New)
            fraudCase.Status = CaseStatus.UnderInvestigation;
        return await _cases.SaveAsync(fraudCase, ct);
    }

    public async Task<FraudCase> AddNoteAsync(Guid caseId, string note, string authorId, CancellationToken ct = default)
    {
        var fraudCase = await GetFraudAsync(caseId, ct);
        // Append-only per FR-20
        var entry = $"[{DateTimeOffset.UtcNow:O} · {authorId}]: {note}\n";
        fraudCase.Notes += entry;
        return await _cases.SaveAsync(fraudCase, ct);
    }

    public async Task<FraudCase> EscalateAsync(Guid caseId, string escalatedBy, CancellationToken ct = default)
    {
        var fraudCase = await GetFraudAsync(caseId, ct);
        fraudCase.Status          = CaseStatus.Escalated;
        fraudCase.LastEscalatedAt = DateTimeOffset.UtcNow;
        fraudCase.Notes += $"[{DateTimeOffset.UtcNow:O} · {escalatedBy}]: Case escalated.\n";
        return await _cases.SaveAsync(fraudCase, ct);
    }

    public async Task<FraudCase> ResolveAsync(
        Guid caseId,
        CaseResolution resolution,
        string resolvedBy,
        CancellationToken ct = default)
    {
        var fraudCase = await GetFraudAsync(caseId, ct);
        fraudCase.Status     = CaseStatus.Resolved;
        fraudCase.Resolution = resolution;
        fraudCase.ResolvedAt = DateTimeOffset.UtcNow;
        fraudCase.Notes +=
            $"[{DateTimeOffset.UtcNow:O} · {resolvedBy}]: Resolved as {resolution}.\n";

        await _cases.SaveAsync(fraudCase, ct);

        // FR-17 / Workflow 5: confirmed fraud triggers SAR + auto-blacklist
        if (resolution == CaseResolution.ConfirmedFraud)
        {
            var alert = await _cases.GetAlertByIdAsync(fraudCase.AlertId, ct);
            if (alert is not null)
            {
                try
                {
                    await _sideEffects.OnConfirmedFraudAsync(fraudCase, alert, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Side-effects for confirmed-fraud case {CaseId} failed; manual follow-up required",
                        caseId);
                }
            }
        }

        return fraudCase;
    }

    public async Task<FraudCase> OverrideVerdictAsync(
        Guid caseId,
        Verdict newVerdict,
        string overriddenBy,
        CancellationToken ct = default)
    {
        var fraudCase = await GetFraudAsync(caseId, ct);
        fraudCase.Notes +=
            $"[{DateTimeOffset.UtcNow:O} · {overriddenBy}]: Verdict overridden to {newVerdict}.\n";
        // The actual transaction unblock/reblock is handled by the middleware — the FMS records
        // the intent so the Admin Console can display the override and the middleware can act.
        return await _cases.SaveAsync(fraudCase, ct);
    }

    private async Task<FraudCase> GetFraudAsync(Guid caseId, CancellationToken ct)
    {
        var c = await _cases.GetByIdAsync(caseId, ct);
        if (c is null) throw new KeyNotFoundException($"Case {caseId} not found.");
        return c;
  