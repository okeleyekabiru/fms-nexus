namespace Nexus.Fms.Core.Domain;

/// <summary>
/// An alert record created for every transaction whose composite score &gt; 0 (§6 fraud_alerts, FR-18).
/// </summary>
public class FraudAlert
{
    public Guid AlertId { get; set; } = Guid.NewGuid();
    public string TransactionRef { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }

    /// <summary>JSON array of triggered rule codes/ids and their individual contributions.</summary>
    public string TriggeredRulesJson { get; set; } = "[]";

    public int CompositeRiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public Verdict Verdict { get; set; }

    /// <summary>NIBSS Fraud Bureau response payload, if a lookup was performed (FR-14).</summary>
    public string? NibssLookupResultJson { get; set; }

    /// <summary>True when verdict came from a shadow-mode evaluation (action not enforced).</summary>
    public bool ShadowOnly { get; set; }

    // ── Transaction party fields (needed for SAR submission — FR-17) �