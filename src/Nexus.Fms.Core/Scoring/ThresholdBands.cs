using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Scoring;

/// <summary>
/// Configurable score-to-risk-level threshold bands (FR-11, FR-12). Administrators can adjust
/// these via the Admin Console to change system sensitivity without touching individual rules.
/// Defaults match the spec table in §2.3.
/// </summary>
public sealed class ThresholdBands
{
    /// <summary>Minimum composite score for P1 – Critical (AUTO-BLOCK). Default 75.</summary>
    public int P1Min { get; set; } = 75;

    /// <summary>Minimum composite score for P2 – High (REQUIRE MFA). Default 50.</summary>
    public int P2Min { get; set; } = 50;

    /// <summary>Minimum composite score for P3 – Medium (FLAG FOR REVIEW). Default 25.</summary>
    public int P3Min { get; set; } = 25;

    /// <summary>Minimum composite score for P4 – Low (LOG ONLY). Default 1.</summary>
    public int P4Min { get; set; } = 1;

    public RiskLevel Classify(int score)
    {
        if (score >= P1Min) return RiskLevel.P1;
        if (score >= P2Min) return RiskLevel.P2;
        if (score >= P3Min) return RiskLevel.P3;
        if (score >= P4Min) return RiskLevel.P4;
        return RiskLevel.Clean;
    }

    public void Validate()
    {
        if (!(P1Min > P2Min && P2Min > P3Min && P3Min > P4Min && P4Min >= 1))
            throw new InvalidOperationException(
                "Threshold bands must be strictly descending and P4Min >= 1 (P1 > P2 > P3 > P4 >= 1).");
    }
}
