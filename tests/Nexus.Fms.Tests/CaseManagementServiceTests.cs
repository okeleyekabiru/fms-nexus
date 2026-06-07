using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Services;
using Xunit;

namespace Nexus.Fms.Tests;

/// <summary>
/// Unit tests for <see cref="CaseManagementService"/> — covers FR-19, FR-20, Workflow 3.
/// Uses Moq for repository dependencies; no EF Core or database required.
/// </summary>
public sealed class CaseManagementServiceTests
{
    private readonly Mock<ICaseRepository> _repoMock = new();
    private readonly Mock<ICaseSideEffectsHandler> _effectsMock = new();
    private readonly CaseManagementService _svc;

    public CaseManagementServiceTests()
    {
        _svc = new CaseManagementService(
            _repoMock.Object,
            _effectsMock.Object,
            NullLogger<CaseManagementService>.Instance);
    }

    private static FraudCase NewCase(Guid? alertId = null) => new()
    {
        CaseId  = Guid.NewGuid(),
        AlertId = alertId ?? Guid.NewGuid(),
        Status  = CaseStatus.New
    };

    private void SetupGetById(FraudCase c) =>
        _repoMock.Setup(r => r.GetByIdAsync(c.CaseId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(c);

    private void SetupGetAlertById(Guid alertId, FraudAlert alert) =>
        _repoMock.Setup(r => r.GetAlertByIdAsync(alertId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(alert);

    private void SetupSave() =>
        _repoMock.Setup(r => r.SaveAsync(It.IsAny<FraudCase>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((FraudCase c, CancellationToken _) => c);

    // FR-20: assign changes status to UnderInvestigation and sets AssignedTo.
    [Fact]
    public async Task Assign_SetsStatusAndAssignee()
    {
        var c = NewCase();
        SetupGetById(c);
        SetupSave();

        var result = await _svc.AssignAsync(c.CaseId, "analyst01");

        result.Status.Should().Be(CaseStatus.UnderInvestigation);
        result.AssignedTo.Should().Be("analyst01");
        _repoMock.Verify(r => r.SaveAsync(c, It.IsAny<CancellationToken>()), Times.Once);
    }

    // FR-20: note is appended with timestamp and author.
    [Fact]
    public async Task AddNote_AppendsNoteWithAuthor()
    {
        var c = NewCase();
        SetupGetById(c);
        SetupSave();

        var result = await _svc.AddNoteAsync(c.CaseId, "Suspicious pattern detected", "analyst01");

        result.Notes.Should().Contain("Suspicious pattern detected");
        result.Notes.Should().Contain("analyst01");
    }

    // FR-20: escalate moves status to Escalated and sets LastEscalatedAt.
    [Fact]
    public async Task Escalate_SetsStatusAndTimestamp()
    {
        var c = NewCase();
        c.Status = CaseStatus.UnderInvestigation;
        SetupGetById(c);
        SetupSave();

        var before = DateTimeOffset.UtcNow;
        var result = await _svc.EscalateAsync(c.CaseId, "supervisor");

        result.Status.Should().Be(CaseStatus.Escalated);
        result.LastEscalatedAt.Should().BeOnOrAfter(before);
    }

    // FR-19: resolve with ConfirmedFraud triggers side effects and sets ResolvedAt.
    [Fact]
    public async Task Resolve_ConfirmedFraud_CallsSideEffectsHandler()
    {
        var alertId = Guid.NewGuid();
        var c = NewCase(alertId);
        var alert = new FraudAlert { AlertId = alertId, TransactionRef = "TX123" };

        SetupGetById(c);
        SetupGetAlertById(alertId, alert);
        SetupSave();
        _effectsMock.Setup(e => e.OnConfirmedFraudAsync(c, alert, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        var result = await _svc.ResolveAsync(c.CaseId, CaseResolution.ConfirmedFraud, "analyst01");

        result.Status.Should().Be(CaseStatus.Resolved);
        result.Resolution.Should().Be(CaseResolution.ConfirmedFraud);
        result.ResolvedAt.Should().NotBeNull();
        _effectsMock.Verify(
            e => e.OnConfirmedFraudAsync(c, alert, It.IsAny<CancellationToken>()), Times.Once);
    }

    // FR-19: resolve with FalsePositive does NOT trigger side effects.
    [Fact]
    public async Task Resolve_FalsePositive_DoesNotCallSideEffectsHandler()
    {
        var c = NewCase();
        SetupGetById(c);
        SetupSave();

        await _svc.ResolveAsync(c.CaseId, CaseResolution.FalsePositive, "analyst01");

        _effectsMock.Verify(
            e => e.OnConfirmedFraudAsync(It.IsAny<FraudCase>(), It.IsAny<FraudAlert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // KeyNotFoundException when case does not exist.
    [Fact]
    public async Task Assign_UnknownCase_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((FraudCase?)null);

        await FluentActions
            .Invoking(() => _svc.AssignAsync(Guid.NewGuid(), "analyst01"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
