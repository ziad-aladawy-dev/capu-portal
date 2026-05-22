using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.CourseOffering.Application;
using CapitalUniversity.Modules.CourseOffering.Application.Validators;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using FluentAssertions;
using Moq;
using Xunit;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Core.UniTests.CourseOfferings;

/// <summary>
/// Service-level lifecycle contract: illegal status / registration-state
/// transitions surface as ConflictException; legal transitions persist; the
/// new GetForCourseAsync query filters cross-node results by per-node scope.
/// </summary>
public class CourseOfferingServiceLifecycleTests
{
    private static (CourseOfferingService Service, Mock<ICourseOfferingRepository> Repo, Mock<IEffectiveScope> Scope) Build(bool inScope = true)
    {
        var repo = new Mock<ICourseOfferingRepository>();
        var uow = new Mock<IUnitOfWork>();
        var scope = new Mock<IEffectiveScope>();
        scope.Setup(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(inScope);
        var sut = new CourseOfferingService(
            uow.Object,
            repo.Object,
            new CreateCourseOfferingValidator(),
            new UpdateCourseOfferingValidator(),
            scope.Object);
        return (sut, repo, scope);
    }

    private static CourseOfferingEntity NewOffering(OfferingStatus initialStatus = OfferingStatus.Draft, int capacity = 10)
    {
        var entity = new CourseOfferingEntity
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
            Status = initialStatus,
        };
        entity.InitializeCapacity(capacity);
        return entity;
    }

    [Fact]
    public async Task Update_ActivatesDraftOffering()
    {
        var (sut, repo, _) = Build();
        var existing = NewOffering(OfferingStatus.Draft);
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Open });

        existing.Status.Should().Be(OfferingStatus.Open);
    }

    [Fact]
    public async Task Update_ReopeningClosedOffering_ThrowsConflict()
    {
        var (sut, repo, _) = Build();
        // Build a Closed offering: start Open then close — Closed cannot be
        // constructed directly via init because Close() also forces reg closed.
        var existing = NewOffering(OfferingStatus.Open);
        existing.Close();
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var act = () => sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Open });
        await act.Should().ThrowAsync<ConflictException>();
        existing.Status.Should().Be(OfferingStatus.Closed);
    }

    [Fact]
    public async Task Update_RevertingToDraft_ThrowsConflict()
    {
        var (sut, repo, _) = Build();
        var existing = NewOffering(OfferingStatus.Open);
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var act = () => sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Draft });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_CancellingForcesRegistrationClosed()
    {
        var (sut, repo, _) = Build();
        var existing = NewOffering(OfferingStatus.Open, capacity: 5);
        existing.OpenRegistration();
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Cancelled });

        existing.Status.Should().Be(OfferingStatus.Cancelled);
        existing.RegistrationState.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public async Task Update_OpeningRegistrationOnDraft_ThrowsConflict()
    {
        var (sut, repo, _) = Build();
        var existing = NewOffering(OfferingStatus.Draft);
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var act = () => sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { RegistrationState = RegistrationState.Open });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_ActivateThenOpenRegistration_InSameRequest_Succeeds()
    {
        var (sut, repo, _) = Build();
        var existing = NewOffering(OfferingStatus.Draft);
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        // Order matters: status flip is applied first so the dependent
        // registration-state guard sees Status=Open.
        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest
        {
            Status = OfferingStatus.Open,
            RegistrationState = RegistrationState.Open,
        });

        existing.Status.Should().Be(OfferingStatus.Open);
        existing.RegistrationState.Should().Be(RegistrationState.Open);
    }

    // ---- New query --------------------------------------------------------

    [Fact]
    public async Task GetForCourseAsync_FiltersByPerNodeScope()
    {
        var courseId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var visibleNode = Guid.NewGuid();
        var hiddenNode = Guid.NewGuid();

        var visible = NewOffering();
        visible.StructureNodeId = visibleNode;
        var hidden = NewOffering();
        hidden.StructureNodeId = hiddenNode;

        var repo = new Mock<ICourseOfferingRepository>();
        var uow = new Mock<IUnitOfWork>();
        var scope = new Mock<IEffectiveScope>();
        scope.Setup(s => s.CanAccessStructureNodeAsync(visibleNode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessStructureNodeAsync(hiddenNode, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.GetForCourseAsync(courseId, semesterId, default))
            .ReturnsAsync(new[] { visible, hidden });

        var sut = new CourseOfferingService(
            uow.Object,
            repo.Object,
            new CreateCourseOfferingValidator(),
            new UpdateCourseOfferingValidator(),
            scope.Object);

        var result = await sut.GetForCourseAsync(courseId, semesterId);

        result.Should().HaveCount(1);
        result[0].StructureNodeId.Should().Be(visibleNode);
    }

    [Fact]
    public async Task GetForNodeSemesterAsync_StatusFilter_PassesThroughToRepo()
    {
        var (sut, repo, _) = Build();
        var nodeId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        repo.Setup(r => r.GetForNodeSemesterAsync(nodeId, semesterId, OfferingStatus.Draft, default))
            .ReturnsAsync(Array.Empty<CourseOfferingEntity>())
            .Verifiable();

        _ = await sut.GetForNodeSemesterAsync(nodeId, semesterId, OfferingStatus.Draft);

        repo.Verify();
    }
}
