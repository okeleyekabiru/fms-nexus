namespace Nexus.Fms.Core.Domain;

/// <summary>
/// The transaction context passed from the NEXUS middleware to the FMS for screening
/// (Workflow 1, step 3). Immutable input to the rule engine.
/// </summary>
public sealed record TransactionContext
{
    public required string TransactionRef { get; init; }
    public Guid CustomerId { get; init; }

    public TransactionType Type { get; init; }
    public TransactionDirection Direction { get; init; }
    public Channel Channel { get; init; }

    public decimal Amount { get; init; }
    public string Currency { get; init; } = "NGN";

    // Parties
    public required string SenderAccount { get; init; }
    public string? SenderBvn { get; init; }
    public string? SenderBank { get; init; }
    public required string ReceiverAccount { get; init; }
    public string? ReceiverBvn { get; init; }
    public string? ReceiverBank { get; init; }
    public string? Narration { get; init; }

    // Device / session signals (FR-09 d)
    public string? DeviceId { get; init; }
    public bool IsNewDevice { get; init; }
    public bool DeviceMatchesRegistered { get; init; } = true;
    public int ConcurrentSessions { get; init; } = 1;
    public TimeSpan? TimeSinceFirstLogin { get; init; }

    public string? IpAddress { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // Authentication signals (FR-09 i)
    public int FailedOtpAttemptsLast15Min { get; init; }

    // Beneficiary signals (FR-09 c)
    public bool IsFirstTimeBeneficiary { get; init; }
    public int SuccessfulTransfersToBeneficiary { get; init; }

    /// <summary>
    /// Pre-aggregated summary of the customer's recent behaviour, supplied by the
    /// middleware so the synchronous path stays within the 50ms budget (NFR-01).
    /// </summary>
    public CustomerHistorySummary History { get; init; } = new();
}

/// <summary>Behavioural baseline used by amount/velocity/balance/behavioural rules.</summary>
public sealed record CustomerHistorySummary
{
    public decimal Percentile95Amount90Day { get; init; }
    public int OutboundTransfersLast10Min { get; init; }
    public decimal CumulativeOutflowToday { get; init; }
    public decimal OpeningBalanceToday { get; init; }
    public int InboundCreditsSimilarAmountLast24h { get; init; }
    public int RoundNumberOutflowsLast24h { get; init; }
    public bool IsWhitelisted { get; init; }
}
