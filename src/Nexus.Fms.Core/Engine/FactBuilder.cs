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
        var h = ctx.History;
        var outflowPercent = h.OpeningBalanceToday > 0
            ? (double)(h.CumulativeOutflowToday / h.OpeningBalanceToday) * 100d
            : 0d;
        var amountToP95Ratio = h.Percentile95Amount90Day > 0
            ? (double)(ctx.Amount / h.Percentile95Amount90Day)
            : 0d;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [FactKeys.Amount] = ctx.Amount,
            [FactKeys.TransactionType] = ctx.Type.ToString(),
            [FactKeys.Direction] = ctx.Direction.ToString(),
            [FactKeys.Channel] = ctx.Channel.ToString(),
            [FactKeys.HourOfDay] = ctx.Timestamp.LocalDateTime.Hour,

            [FactKeys.Percentile95Amount] = h.Percentile95Amount90Day,
            [FactKeys.AmountToP95Ratio] = amountToP95Ratio,
            [FactKeys.OutboundTransfersLast10Min] = h.OutboundTransfersLast10Min,
            [FactKeys.CumulativeOutflowToday] = h.CumulativeOutflowToday,
            [FactKeys.OpeningBalanceToday] = h.OpeningBalanceToday,
            [FactKeys.OutflowPercentOfBalance] = outflowPercent,
            [FactKeys.InboundCreditsSimilarAmountLast24h] = h.InboundCreditsSimilarAmountLast24h,
            [FactKeys.RoundNumberOutflowsLast24h] = h.RoundNumberOutflowsLast24h,

            [FactKeys.IsFirstTimeBeneficiary] = ctx.IsFirstTimeBeneficiary,
            [FactKeys.SuccessfulTransfersToBeneficiary] = ctx.SuccessfulTransfersToBeneficiary,

            [FactKeys.IsNewDevice] = ctx.IsNewDevice,
            [FactKeys.DeviceMatchesRegistered] = ctx.DeviceMatchesRegistered,
            [FactKeys.ConcurrentSessions] = ctx.ConcurrentSessions,
            [FactKeys.MinutesSinceFirstLogin] = ctx.TimeSinceFirstLogin?.TotalMinutes ?? double.MaxValue,

            [FactKeys.FailedOtpAttemptsLast15Min] = ctx.FailedOtpAttemptsLast15Min,

            [FactKeys.IsWhitelisted] = lookups.IsWhitelisted || h.IsWhitelisted,
            [FactKeys.SenderOnInternalBlacklist] = lookups.SenderOnInternalBlacklist,
            [FactKeys.ReceiverOnNibssWatchlist] = lookups.ReceiverOnNibssWatchlist,
            [FactKeys.ReceiverNibssConfirmedFraud] = lookups.ReceiverNibssConfirmedFraud,
            [FactKeys.SenderOnNibssWatchlist] = lookups.SenderOnNibssWatchlist,
            [FactKeys.NibssLookupUnavailable] = lookups.NibssLookupUnavailable,
        };
    }
}
