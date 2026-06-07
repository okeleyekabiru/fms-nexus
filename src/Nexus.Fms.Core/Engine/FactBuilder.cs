using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Engine;

/// <summary>Result of the watchlist/list lookups the screening service performs before rule evaluation.</summary>
public sealed record ListLookupResult
{
    public bool IsWhitelisted { get; init; }
    public bool SenderOnInternalBlacklist { get; init; }
    public bool ReceiverOnNibssWatchlist { get; init; }
    public bool ReceiverNibssConfirmedFraud { get; init; }
    public bool SenderOnNibssWatchlist { get; init; }
    public bool NibssLookupUnavailable { get; init; }
}

/// <summary>
/// Flattens a <see cref="TransactionContext"/> plus lookup results into the fact dictionary
/// consumed by <see cref="PredicateEvaluator"/>. Single source of truth for the FR-09 signals.
/// </summary>
public static class FactBuilder
{
    public static Dictionary<string, object?> Build(TransactionContext ctx, ListLookupResult lookups)
    {
        var history = ctx.History;
        var outflowPercent = history.OpeningBalanceToday > 0
            ? (double)(history.CumulativeOutflowToday / history.OpeningBalanceToday) * 100d
            : 0d;
        var amountToP95Ratio = history.Percentile95Amount90Day > 0
            ? (double)(ctx.Amount / history.Percentile95Amount90Day)
            : 0d;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [FactKeys.Amount] = ctx.Amount,
            [FactKeys.TransactionType] = ctx.Type.ToString(),
            [FactKeys.Direction] = ctx.Direction.ToString(),
            [FactKeys.Channel] = ctx.Channel.ToString(),
            [FactKeys.HourOfDay] = ctx.Timestamp.LocalDateTime.Hour,

            [FactKeys.Percentile95Amount] = history.Percentile95Amount90Day,
            [FactKeys.AmountToP95Ratio] = amountToP95Ratio,
            [FactKeys.OutboundTransfersLast10Min] = history.OutboundTransfersLast10Min,
            [FactKeys.CumulativeOutflowToday] = history.CumulativeOutflowToday,
            [FactKeys.OpeningBalanceToday] = history.OpeningBalanceToday,
            [FactKeys.OutflowPercentOfBalance] = outflowPercent,
            [FactKeys.InboundCreditsSimilarAmountLast24h] = history.InboundCreditsSimilarAmountLast24h,
            [FactKeys.RoundNumberOutflowsLast24h] = history.RoundNumberOutflowsLast24h,

            [FactKeys.IsFirstTimeBeneficiary] = ctx.IsFirstTimeBeneficiary,
            [FactKeys.SuccessfulTransfersToBeneficiary] = ctx.SuccessfulTransfersToBeneficiary,

            [FactKeys.IsNewDevice] = ctx.IsNewDevice,
            [FactKeys.DeviceMatchesRegistered] = ctx.DeviceMatchesRegistered,
            [FactKeys.ConcurrentSessions] = ctx.ConcurrentSessions,
            [FactKeys.MinutesSinceFirstLogin] = ctx.TimeSinceFirstLogin?.TotalMinutes ?? double.MaxValue,

            [FactKeys.FailedOtpAttemptsLast15Min] = ctx.FailedOtpAttemptsLast15Min,

            [FactKeys.IsWhitelisted] = lookups.IsWhitelisted || history.IsWhitelisted,
            [FactKeys.SenderOnInternalBlacklist] = lookups.SenderOnInternalBlacklist,
            [FactKeys.ReceiverOnNibssWatchlist] = lookups.ReceiverOnNibssWatchlist,
            [FactKeys.ReceiverNibssConfirmedFraud] = lookups.ReceiverNibssConfirmedFraud,
            [FactKeys.SenderOnNibssWatchlist] = lookups.SenderOnNibssWatchlist,
            [FactKeys.NibssLookupUnavailable] = lookups.NibssLookupUnavailable,
        };
    }
}
