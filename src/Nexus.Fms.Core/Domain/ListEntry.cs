namespace Nexus.Fms.Core.Domain;

/// <summary>
/// A blacklist or whitelist entry keyed by BVN/account (§6 fraud_blacklist / whitelist, FR-28).
/// </summary>
public class ListEntry
{
    public Guid EntryId { get; set; } = Guid.NewGuid();

    public string? Bvn { get; set; }
    public string? AccountNumber { get; set; }

    public ListType ListType { get; set; }
    public ListSource Source { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
