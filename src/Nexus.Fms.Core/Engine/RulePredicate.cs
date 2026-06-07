using System.Text.Json.Serialization;

namespace Nexus.Fms.Core.Engine;

/// <summary>
/// Structured, serialisable predicate used as a rule's condition (stored in
/// FraudRule.ConditionsJson). Supports a recursive AND/OR tree of leaf comparisons,
/// which lets administrators author arbitrary conditions without code (FR-05, FR-08, NFR-07).
///
/// A leaf compares a named <see cref="Fact"/> against a <see cref="Value"/> using an
/// <see cref="Op"/>. Facts are supplied by the screening service (see FactKeys) and cover
/// all nine condition types in FR-09.
/// </summary>
public sealed class RulePredicate
{
    /// <summary>Logical combinator. When set, <see cref="Children"/> is used and leaf fields are ignored.</summary>
    [JsonPropertyName("all")]
    public List<RulePredicate>? All { get; set; }

    [JsonPropertyName("any")]
    public List<RulePredicate>? Any { get; set; }

    [JsonPropertyName("not")]
    public RulePredicate? Not { get; set; }

    // Leaf comparison
    [JsonPropertyName("fact")]
    public string? Fact { get; set; }

    [JsonPropertyName("op")]
    public PredicateOp? Op { get; set; }

    [JsonPropertyName("value")]
    public System.Text.Json.JsonElement? Value { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PredicateOp
{
    Eq,
    Ne,
    Gt,
    Gte,
    Lt,
    Lte,
    In,
    IsTrue,
    IsFalse
}

/// <summary>Canonical fact keys produced by the screening service for predicate evaluation.</summary>
public static class FactKeys
{
    public const string Amount = "amount";
    public const string TransactionType = "transactionType";
    public const string Direction = "direction";
    public const string Channel = "channel";
    public const string HourOfDay = "hourOfDay";

    public const string Percentile95Amount = "percentile95Amount";
    public const string AmountToP95Ratio = "amountToP95Ratio";
    public const string OutboundTransfersLast10Min = "outboundTransfersLast10Min";
    public const string CumulativeOutflowToday = "cumulativeOutflowToday";
    public const string OpeningBalanceToday = "openingBalanceToday";
    public const string OutflowPercentOfBalance = "outflowPercentOfBalance";
    public const string InboundCreditsSimilarAmountLast24h = "inboundCreditsSimilarAmountLast24h";
    public const string RoundNumberOutflowsLast24h = "roundNumberOutflowsLast24h";

    public const string IsFirstTimeBeneficiary = "isFirstTimeBeneficiary";
    public const string SuccessfulTransfersToBeneficiary = "successfulTransfersToBeneficiary";

    public const string IsNewDevice = "isNewDevice";
    public const string DeviceMatchesRegistered = "deviceMatchesRegistered";
    public const string ConcurrentSessions = "concurrentSessions";
    public const string MinutesSinceFirstLogin = "minutesSinceFirstLogin";

    public const string FailedOtpAttemptsLast15Min = "failedOtpAttemptsLast15Min";

    public const string IsWhitelisted = "isWhitelisted";
    public const string SenderOnInternalBlacklist = "senderOnInternalBlacklist";
    public const string ReceiverOnNibssWatchlist = "receiverOnNibssWatchlist";
    public const string ReceiverNibssConfirmedFraud = "receiverNibssConfirmedFraud";
    public const string SenderOnNibssWatchlist = "senderOnNibssWatchlist";
    public const string NibssLookupUnavailable = "nibssLookupUnavailable";
}
