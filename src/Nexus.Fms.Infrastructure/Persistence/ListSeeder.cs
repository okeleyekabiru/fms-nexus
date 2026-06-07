using Microsoft.EntityFrameworkCore;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Persistence;

/// <summary>
/// Seeds sample blacklist/whitelist entries (FR-28) so the screening pipeline can be exercised
/// end-to-end in development. These identifiers line up with the sample requests in the .http file.
/// </summary>
public static class ListSeeder
{
    public static async Task SeedAsync(FmsDbContext db, CancellationToken ct = default)
    {
        if (await db.ListEntries.AnyAsync(ct)) return;

        db.ListEntries.AddRange(
            new ListEntry
            {
                Bvn = "BLACKLISTED-BVN",
                ListType = ListType.Blacklist,
                Source = ListSource.Internal,
                Reason = "Sample confirmed-fraud entry (dev seed) — triggers R09 (+100, auto-block).",
                CreatedBy = "system-seed"
            },
            new ListEntry
            {
                Bvn = "WHITELISTED-BVN",
                ListType = ListType.Whitelist,
                Source = ListSource.Internal,
                Reason = "Sample trusted customer (dev seed) — triggers R13 (-10 mitigant).",
                CreatedBy = "system-seed"
            });

        await db.SaveChangesAsync(ct);
    }
}
