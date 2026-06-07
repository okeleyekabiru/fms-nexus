using System.ComponentModel.DataAnnotations;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Contracts;

/// <summary>
/// Wire contract the NEXUS middleware posts to /api/screening/evaluate (Workflow 1, step 3).
/// Maps to the domain <see cref="TransactionContext"/>.
/// </summary>
public sealed class ScreenTransactionRequest
{
    [Required] public string TransactionRef { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }

    public TransactionType Type { get; set; }
    public TransactionDirection Direction { get; set; }
    public Channel Channel { get; set; }

    [Range(0, double.MaxValue)] public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";

    [Required] public string SenderAccount { get; set; } = string.Empty;
    public string? SenderBvn { get; set; }
    public string? SenderBank { get; set; }
    [Required] public string ReceiverAccount { get; set; } = string.Empty;
    public string? ReceiverBvn { get; set; }
    public string? ReceiverBank { get; set; }
    public string? Narration { get; set; }

    public string? DeviceId { get; set; }
    public bool IsNewDevice { get; set; }
    public bool DeviceMatchesRegistered { get; set; } = true;
    public int ConcurrentSessions { get; set; } = 1;
    public double? MinutesSinceFirstLogin { get; set; }

    public string? IpAddress { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    public int FailedOtpAttemptsLast15Min { get; set; }
    public bool IsFirstTimeBeneficiary { get; set; }
    public int SuccessfulTransfersToBeneficiary { get; set; }

    public CustomerHistoryDto History { get; set; } = new();

    public TransactionContext ToDomain() => new()
    {
        TransactionRef = TransactionRef,
        CustomerId = CustomerId,
        Type = Type,
        Direction = Direction,
        Channel = Channel,
        Amount = Amount,
        Currency = Currency,
        SenderAccount = SenderAccount,
        SenderBvn = SenderBvn,
        SenderBank = SenderBank,
        ReceiverAccount = ReceiverAccount,
        ReceiverBvn = ReceiverBvn,
        ReceiverBank = ReceiverBank,
        Narration = Narration,
        DeviceId = DeviceId,
        IsNewDevice = IsNewDevice,
        DeviceMatchesRegistered = DeviceMatchesRegistered,
        ConcurrentSessions = ConcurrentSessions,
        TimeSinceFirstLogin = MinutesSinceFirstLogin is { } m ? TimeSpan.FromMinutes(m) : null,
        IpAddress = IpAddress,
        Timestamp = Timestamp ?? DateTimeOffset.UtcNow,
        FailedOtpAttemptsLast15Min = FailedOtpAttemptsLast15Min,
        IsFirstTimeBeneficiary = IsFirstTimeBeneficiary,
        SuccessfulTransfersToBeneficiary = SuccessfulTransfersToBeneficiary,
        History = new CustomerHistorySummary
        {
            Percentile95Amount90Day = History.Percentile95Amount90Day,
            OutboundTransfersLast10Min = History.OutboundTransfersLast10Min,
            CumulativeOutflowToday = History.CumulativeOutflowToday,
            OpeningBalanceToday = History.OpeningBalanceToday,
            InboundCreditsSimilarAmountLast24h = History.InboundCreditsSimilarAmountLast24h,
            RoundNumberOutflowsLast24h = History.RoundNumberOutflowsLast24h,
            IsWhitelisted = History.IsWhitelisted
        }
    };
}

public sealed class CustomerHistoryDto
{
    public decimal Percentile95Amount90Day { get; set; }
    public int OutboundTransfersLast10Min { get; set; }
    public decimal CumulativeOutflowToday { get; set; }
    public decimal OpeningBalanceToday { get; set; }
    public int InboundCreditsSimilarAmountLast24h { get; set; }
    public int RoundNumberOutflowsLast24h { get; set; }
    public bool IsWhitelisted { get; set; }
}
