namespace Nexus.Fms.Core.Dto;

/// <summary>SAR payload submitted to NIBSS when fraud is confirmed (Workflow 5).</summary>
public sealed record SarPayload
{
    public required string Bvn { get; init; }
    public required string AccountNumber { get; init; }
    public required string TransactionRef { get; init; }
    public required string FraudType { get; init; }
    public required decimal Amount { get; init; }
    public required DateTimeOffset Date { get; init; }
    public string? Narrative { get; init; }
}
