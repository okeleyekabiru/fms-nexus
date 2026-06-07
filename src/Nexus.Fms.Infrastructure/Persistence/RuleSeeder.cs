using Microsoft.EntityFrameworkCore;
using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Infrastructure.Persistence;

/// <summary>
/// Seeds the initial 15-rule set from the requirements spec (§5). Thresholds are illustrative
/// and intended to be calibrated in shadow mode before go-live (Assumptions §7c).
/// All rules are seeded in SHADOW mode so nothing is enforced until an admin promotes them.
/// </summary>
public static class RuleSeeder
{
    public static async Task SeedAsync(FmsDbContext db, RuleMode mode = RuleMode.Shadow, CancellationToken ct = default)
    {
        if (await db.Rules.AnyAsync(ct)) return;

        db.Rules.AddRange(BuildInitialRules(mode));
        await db.SaveChangesAsync(ct);
    }

    public static IEnumerable<FraudRule> BuildInitialRules(RuleMode mode = RuleMode.Shadow)
    {
        FraudRule Rule(string code, string name, RuleCategory cat, int score, string json,
            bool sync = true, bool noOffset = false) => new()
        {
            Code = code,
            Name = name,
            Category = cat,
            Score = score,
            ConditionsJson = json,
            Mode = mode,
            IsSynchronous = sync,
            CannotBeOffset = noOffset,
            CreatedBy = "system-seed",
            ApprovedBy = "system-seed"
        };

        return
        [
            Rule("R01", "Amount exceeds customer 95th percentile", RuleCategory.Amount, 40,
                """{ "fact": "amountToP95Ratio", "op": "Gt", "value": 2 }"""),

            Rule("R02", "Velocity: >5 transfers in 10 minutes", RuleCategory.Velocity, 50,
                """{ "fact": "outboundTransfersLast10Min", "op": "Gt", "value": 5 }"""),

            Rule("R03", "Beneficiary on NIBSS fraud watchlist", RuleCategory.Watchlist, 50,
                """{ "all": [ { "fact": "receiverOnNibssWatchlist", "op": "IsTrue" }, { "not": { "fact": "receiverNibssConfirmedFraud", "op": "IsTrue" } } ] }"""),

            Rule("R03B", "Beneficiary is NIBSS confirmed fraud", RuleCategory.Watchlist, 100,
                """{ "fact": "receiverNibssConfirmedFraud", "op": "IsTrue" }""", noOffset: true),

            Rule("R04", "First-time beneficiary + amount > N500K", RuleCategory.Beneficiary, 40,
                """{ "all": [ { "fact": "isFirstTimeBeneficiary", "op": "IsTrue" }, { "fact": "amount", "op": "Gt", "value": 500000 } ] }"""),

            Rule("R05", "New device + transfer within 30 min of first login", RuleCategory.Device, 30,
                """{ "all": [ { "fact": "isNewDevice", "op": "IsTrue" }, { "fact": "minutesSinceFirstLogin", "op": "Lt", "value": 30 } ] }"""),

            Rule("R06", "Transaction outside 11PM-5AM", RuleCategory.Time, 15,
                """{ "any": [ { "fact": "hourOfDay", "op": "Gte", "value": 23 }, { "fact": "hourOfDay", "op": "Lt", "value": 5 } ] }"""),

            Rule("R07", "Multiple failed OTP then successful transfer", RuleCategory.Auth, 60,
                """{ "fact": "failedOtpAttemptsLast15Min", "op": "Gte", "value": 3 }"""),

            Rule("R08", "Cumulative daily outflow > 80% of balance", RuleCategory.Balance, 35,
                """{ "fact": "outflowPercentOfBalance", "op": "Gt", "value": 80 }"""),

            Rule("R09", "Sender on internal blacklist (confirmed fraud)", RuleCategory.Blacklist, 100,
                """{ "fact": "senderOnInternalBlacklist", "op": "IsTrue" }""", noOffset: true),

            Rule("R10", "Inbound credit from watchlisted sender", RuleCategory.Inbound, 30,
                """{ "all": [ { "fact": "direction", "op": "Eq", "value": "Inflow" }, { "fact": "senderOnNibssWatchlist", "op": "IsTrue" } ] }"""),

            Rule("R11", "Structured deposits (smurfing pattern)", RuleCategory.Inbound, 25,
                """{ "fact": "inboundCreditsSimilarAmountLast24h", "op": "Gte", "value": 3 }""", sync: false),

            Rule("R12", "Round-number transfers >3x in 24hrs", RuleCategory.Pattern, 10,
                """{ "fact": "roundNumberOutflowsLast24h", "op": "Gt", "value": 3 }""", sync: false),

            Rule("R13", "Customer is whitelisted", RuleCategory.Mitigant, -10,
                """{ "fact": "isWhitelisted", "op": "IsTrue" }"""),

            Rule("R14", "Transaction to previously successful beneficiary", RuleCategory.Mitigant, -5,
                """{ "fact": "successfulTransfersToBeneficiary", "op": "Gte", "value": 3 }"""),

            Rule("R15", "NIBSS lookup unavailable", RuleCategory.Compensating, 5,
                """{ "fact": "nibssLookupUnavailable", "op": "IsTrue" }"""),
        ];
    }
}
