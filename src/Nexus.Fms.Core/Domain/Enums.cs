namespace Nexus.Fms.Core.Domain;

/// <summary>Risk level bands. Derived from composite score, not assigned per rule (FR-06, FR-11).</summary>
public enum RiskLevel
{
    Clean = 0,
    P4 = 4, // Low      – LOG ONLY
    P3 = 3, // Medium    – FLAG FOR REVIEW
    P2 = 2, // High      – REQUIRE MFA
    P1 = 1  // Critical  – AUTO-BLOCK
}

/// <summary>Verdict returned to the middleware for a screened transaction (Workflow 1, step 5).</summary>
public enum Verdict
{
    Allow,
    Flag,
    RequireMfa,
    Block
}

/// <summary>Rule deployment mode (FR-07, Workflow 4).</summary>
public enum RuleMode
{
    Disabled, // paused, not evaluated
    Shadow,   // evaluated and logged but action NOT enforced
    Live      // evaluated and action enforced
}

/// <summary>Category of a fraud rule condition (FR-09).</summary>
public enum RuleCategory
{
    Amount,
    Velocity,
    Beneficiary,
    Device,
    Time,
    Behavioural,
    Watchlist,
    Balance,
    Auth,
    Inbound,
    Pattern,
    Blacklist,
    Mitigant,
    Compensating
}

/// <summary>Transaction types in scope (§1.2).</summary>
public enum TransactionType
{
    IntraBankTransfer,
    InterBankTransferNip,
    InboundNipCredit,
    BillPayment,
    LoanDisbursement,
    LoanRepayment,
    PosTransaction
}

public enum TransactionDirection
{
    Inflow,
    Outflow
}

public enum Channel
{
    Adspire,
    Ussd,
    Admin,
    Nibss,
    PosTerminal,
    Auto
}

/// <summary>Fraud case lifecycle (FR-19).</summary>
public enum CaseStatus
{
    New,
    UnderInvestigation,
    Escalated,
    Resolved
}

/// <summary>Case resolution outcomes (FR-19, Workflow 3).</summary>
public enum CaseResolution
{
    ConfirmedFraud,
    FalsePositive,
    Inconclusive
}

public enum ListType
{
    Blacklist,
    Whitelist
}

public enum ListSource
{
    Internal,
    Nibss
}

/// <summary>Behaviour when the FMS or a dependency is unavailable (FR-04, NFR-04).</summary>
public enum FailureMode
{
    FailOpen,   // allow the transaction, log the bypass (default)
    FailClosed  // block the transaction
}
