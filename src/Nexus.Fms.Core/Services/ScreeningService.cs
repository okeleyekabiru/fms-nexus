using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Dto;
using Nexus.Fms.Core.Engine;
using Nexus.Fms.Core.Scoring;

namespace Nexus.Fms.Core.Services;

public sealed class ScreeningOptions
{
    /// <summary>Default fail behaviour when screening errors/times out (FR-04, NFR-04).</summary>
    public FailureMode FailureMode { get; set; } = FailureMode.FailOpen;

    /// <summary>
    /// Per-category overrides for fail-closed behaviour (FR-04).
    /// Categories listed here always fail-closed regardless of <see cref="FailureMode"/>.
    /// Example: ["Blacklist", "Watchlist"]
    /// </summary>
    public List<RuleCategory> FailClosedCategories { get; set; } = new();

    /// <summary>Score added when NIBSS is unreachable (FR-16, R15). Default +5.</summary>
    public int NibssUnavailableCompensatingScore { get; set; } = 5;

    /// <summary>Risk-level threshold (inclusive) at and above which a case is opened (FR-18). Default P3.</summary>
    public RiskLevel CaseCreationLevel { get; set; } = RiskLevel.P3;

    /// <summary>
    /// Returns the effective FailureMode for a set of triggered rule categories (FR-04).
    /// If any triggered category is in <see cref="FailClosedCategories"/>, returns FailClosed.
    /// </summary>
    public FailureMode EffectiveModeFor(IEnumerable<RuleCategory> triggeredCategories) =>
        triggeredCategories.Any(c => FailClosedCategories.Contains(c))
            ? FailureMode.FailClosed
            : FailureMode;
}

/// <summary>
/// Orchestrates Workflow 1: gather facts, run list/NIBSS lookups, evaluate rules, score,
/// create alert/case, and return a verdict. Wraps everything in fail-open/fail-closed handling (FR-04).
/// </summary>
public sealed class ScreeningService : IScreeningService
{
    private readonly IRuleRepository _rules;
    private readonly IListRepository _lists;
    private readonly INibssFraudBureauClient _nibss;
    private readonly IAlertStore _alerts;
    private readonly IAsyncEvaluationQueue _asyncQueue;
    private readonly RuleEngine _engine;
    private readonly ScoringEngine _scoring;
    private readonly ScreeningOptions _options;
    private readonly ILogger<ScreeningService> _logger;

    public ScreeningService(
        IRuleRepository rules,
        IListRepository lists,
        INibssFraudBureauClient nibss,
        IAlertStore alerts,
        IAsyncEvaluationQueue asyncQueue,
        RuleEngine engine,
        ScoringEngine scoring,
        IOptions<ScreeningOptions> options,
        ILogger<ScreeningService> logger)
    {
        _rules = rules;
        _lists = lists;
        _nibss = nibss;
        _alerts = alerts;
        _asyncQueue = asyncQueue;
        _engine = engine;
        _scoring = scoring;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScreeningResponse> ScreenAsync(TransactionContext context, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await ScreenCoreAsync(context, sw, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            // FR-04: on a hard failure we have no triggered categories, so fall back to the
            // global FailureMode. Per-category overrides (EffectiveModeFor) apply only when
            // rules have been evaluated successfully and categories are known.
            _logger.LogError(ex, "Screening failed for {Ref}; applying {Mode}", context.TransactionRef, _options.FailureMode);
            var verdict = _options.FailureMode == FailureMode.FailClosed ? Verdict.Block : Verdict.Allow;
            return new ScreeningResponse
            {
                TransactionRef = context.TransactionRef,
                Verdict = verdict,
                RiskScore = 0,
                RiskLevel = RiskLevel.Clean,
                TriggeredRules = Array.Empty<TriggeredRuleDto>(),
                Bypassed = true,
                EvaluationMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<ScreeningResponse> ScreenCoreAsync(TransactionContext context, Stopwatch sw, CancellationToken ct)
    {
        // 1. Lookups. NIBSS runs in parallel for inter-bank txns (Workflow 1 step 4); the two
        // internal list checks share a scoped DbContext, so they must NOT run concurrently
        // (EF Core forbids concurrent operations on one context instance).
        var needsNibss = context.Type is TransactionType.InterBankTransferNip or TransactionType.InboundNipCredit;
        var receiverNibssTask = needsNibss
            ? _nibss.LookupAsync(context.ReceiverBvn, context.ReceiverAccount, ct)
            : Task.FromResult(NibssLookupResult.NotFound);
        var senderNibssTask = needsNibss
            ? _nibss.LookupAsync(context.SenderBvn, context.SenderAccount, ct)
            : Task.FromResult(NibssLookupResult.NotFound);

        // Sequential DB reads on the shared context.
        var isWhitelisted = await _lists.IsWhitelistedAsync(context.SenderBvn, context.SenderAccount, ct);
        var isBlacklisted = await _lists.IsBlacklistedAsync(context.SenderBvn, context.SenderAccount, ct);

        // NIBSS uses its own HttpClient, safe to await together.
        await Task.WhenAll(receiverNibssTask, senderNibssTask);
        var receiverNibss = receiverNibssTask.Result;
        var senderNibss = senderNibssTask.Result;
        var nibssUnavailable = needsNibss && (receiverNibss.Unavailable || senderNibss.Unavailable);

        var lookups = new ListLookupResult
        {
            IsWhitelisted = isWhitelisted,
            SenderOnInternalBlacklist = isBlacklisted,
            ReceiverOnNibssWatchlist = receiverNibss.OnWatchlist,
            ReceiverNibssConfirmedFraud = receiverNibss.ConfirmedFraud,
            SenderOnNibssWatchlist = senderNibss.OnWatchlist,
            NibssLookupUnavailable = nibssUnavailable
        };

        // 2. Build facts and evaluate synchronous rules.
        var facts = FactBuilder.Build(context, lookups);
        var activeRules = await _rules.GetActiveRulesAsync(ct);
        var hasAsyncRules = activeRules.Any(r => r.IsActive && !r.IsSynchronous);
        var triggered = _engine.Evaluate(activeRules, facts, synchronousOnly: true);

        // 3. Score and map to a verdict.
        var result = _scoring.Score(triggered);

        // 4. Persist alert + case when warranted.
        Guid? alertId = null;
        Guid? caseId = null;
        if (result.CompositeScore > 0)
        {
            // M1-1 bug fix: mark alert as shadow-only when no live rules fired (FR-07, Workflow 4)
            var shadowOnly = result.EffectiveRules.Count == 0 && result.ShadowRules.Count > 0;

            var alert = await _alerts.SaveAlertAsync(new FraudAlert
            {
                TransactionRef = context.TransactionRef,
                CustomerId = context.CustomerId,
                CompositeRiskScore = result.CompositeScore,
                RiskLevel = result.RiskLevel,
                Verdict = result.Verdict,
                ShadowOnly = shadowOnly,
                SenderAccount = context.SenderAccount,
                SenderBvn = context.SenderBvn,
                Amount = context.Amount,
                TriggeredRulesJson = JsonSerializer.Serialize(
                    result.EffectiveRules.Select(r => new
                    {
                        r.Code, r.Name, r.Score, r.CannotBeOffset,
                        Category = r.Category.ToString()
                    })),
                NibssLookupResultJson = needsNibss
                    ? JsonSerializer.Serialize(new { receiver = receiverNibss, sender = senderNibss })
                    : null
            }, ct);
            alertId = alert.AlertId;

            // FR-18: open a case for transactions at or above the configured level (default P3).
            if (Severity.IsAtLeast(result.RiskLevel, _options.CaseCreationLevel))
            {
                var fraudCase = await _alerts.CreateCaseAsync(new FraudCase { AlertId = alert.AlertId }, ct);
                caseId = fraudCase.CaseId;
            }

            // FR-03: enqueue async evaluation if any active async rules exist.
            if (hasAsyncRules)
            {
