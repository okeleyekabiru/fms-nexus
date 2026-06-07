namespace Nexus.Fms.Core.Domain;

/// <summary>
/// Severity ranking for <see cref="RiskLevel"/>. The enum's numeric values are not ordered by
/// severity, so comparisons must go through this helper. Higher rank = more severe.
/// </summary>
public static class Severity
{
    public static int Rank(RiskLevel level) => level switch
    {
        RiskLevel.P1 => 4,
        RiskLevel.P2 => 3,
        RiskLevel.P3 => 2,
        RiskLevel.P4 => 1,
        _ => 0 // Clean
    };

    /// <summary>True when <paramref name="level"/> is at least as severe as <paramref name="threshold"/>.</summary>
    public static bool IsAtLeast(RiskLevel level, RiskLevel threshold) => Rank(level) >= Rank(threshold);
}
