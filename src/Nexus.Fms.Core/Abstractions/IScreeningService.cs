using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Dto;

namespace Nexus.Fms.Core.Abstractions;

/// <summary>The screening entry point invoked by the NEXUS middleware (Workflow 1, step 3-5).</summary>
public interface IScreeningService
{
    Task<ScreeningResponse> ScreenAsync(TransactionContext context, CancellationToken ct = default);
}
