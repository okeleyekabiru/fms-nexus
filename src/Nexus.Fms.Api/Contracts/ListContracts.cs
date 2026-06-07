using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Api.Contracts;

// ── Requests ───────────────────────────────────────────────────────────────────

public sealed record AddListEntryRequest(
    string? Bvn,
    string? AccountNumber,
    ListType ListType,
    string Reason);

// ── Responses ──────────────────────────────────────────────────────────────────

public sealed record ListEntryDto
{
    public Guid EntryId { get; init; }
    public string? Bvn { get; init; }
    public string? AccountNumber { get; init; }
    public string ListType { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public static ListEntryDto From(ListEntry e) => new()
    {
        EntryId       = e.EntryId,
        Bvn           = e.Bvn,
        AccountNumber = e.AccountNumber,
        ListType      = e.ListType.ToString(),
        Source        = e.Source.ToString(),
        Reason        = e.Reason,
        CreatedBy     = e.CreatedBy,
        CreatedAt     = e.CreatedAt
    };
}
