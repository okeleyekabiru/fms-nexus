using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>
/// Handles side effects when a case is resolved as ConfirmedFraud (FR-17, Workflow 5):
///   1. Submit SAR to NIBSS Fraud Bureau.
///   2. Auto-add sender BVN/account to internal blacklist.
/// Implementations in Nexus.Fms.Infrastructure.
/// </summary>
public interface ICaseSideEffectsHandler
{
    Task OnConfirmedFraudAsync(FraudCase fraudCase, FraudAlert alert, CancellationToken ct = default);
}
