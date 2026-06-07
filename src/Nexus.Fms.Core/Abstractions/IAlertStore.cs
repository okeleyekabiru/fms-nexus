using Nexus.Fms.Core.Domain;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>Persists alerts and cases (FR-18, FR-19).</summary>
public interface IAlertStore
{
    Task<FraudAlert> SaveAlertAsync(FraudAlert alert, CancellationToken ct = default);
    Task<FraudCase> CreateCaseAsync(FraudCase fraudCase, CancellationToken ct = default);

 