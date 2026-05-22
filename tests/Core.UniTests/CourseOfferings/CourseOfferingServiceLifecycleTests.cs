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
        scope.Setup(s => s.CanAccessSemesterAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(inScope);
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
    public async Task Activate_FromClosed_Throws()
    {
        var offering = NewOffering(OfferingStatus.Closed);
        var (sut, repo, _) = Build();
        repo.Setup(r => r.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);

        // Reopening a closed offering must be a new-offering decision, not a state flip.
        var act = () => sut.UpdateAsync(offering.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Open });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_OpeningRegistrationOnDraft_ThrowsConflict()
    {
        var offering = NewOffering(OfferingStatus.Draft);
        var (sut, repo, _) = Build();
        repo.Setup(r => r.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);

        var act = () => sut.UpdateAsync(offering.Id, new UpdateCourseOfferingRequest { RegistrationState = RegistrationState.Open });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_SetWaitlistOnCancelled_ThrowsConflict()
    {
        var offering = NewOffering(OfferingStatus.Cancelled);
        var (sut, repo, _) = Build();
        repo.Setup(r => r.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);

        var act = () => sut.UpdateAsync(offering.Id, new UpdateCourseOfferingRequest { RegistrationState = RegistrationState.Waitlist });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Close_ClosesRegistrationAutomatically()
    {
        var offering = NewOffering(OfferingStatus.Open);
        offering.OpenRegistration();
        var (sut, repo, _) = Build();
        repo.Setup(r => r.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);

        await sut.UpdateAsync(offering.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Closed });

        offering.Status.Should().Be(OfferingStatus.Closed);
        offering.RegistrationState.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public async Task Cancel_ClosesRegistrationAutomatically()
    {
        var offering = NewOffering(OfferingStatus.Open);
        offering.OpenRegistration();
        var (sut, repo, _) = Build();
        repo.Setup(r => r.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);

        await sut.UpdateAsync(offering.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Cancelled });

        offering.Status.Should().Be(OfferingStatus.Cancelled);
        offering.RegistrationState.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public async Task GetForCourseAsync_FiltersByPerNodeScope()
    {
        var courseId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var (sut, repo, scope) = Build();

        var inScope = NewOffering(); 
        inScope.CourseId = courseId;
        inScope.SemesterId = semesterId;
        
        var outOfScope = NewOffering();
        outOfScope.CourseId = courseId;
        outOfScope.SemesterId = semesterId;

        repo.Setup(r => r.GetForCourseAsync(courseId, semesterId, default))
            .ReturnsAsync(new[] { inScope, outOfScope });

        scope.Setup(s => s.CanAccessStructureNodeAsync(inScope.StructureNodeId, default)).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessStructureNodeAsync(outOfScope.StructureNodeId, default)).ReturnsAsync(false);
        scope.Setup(s => s.CanAccessSemesterAsync(semesterId, default)).ReturnsAsync(true);

        var result = await sut.GetForCourseAsync(courseId, semesterId);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(inScope.Id);
    }

    [Fact]
    public async Task GetForNodeSemesterAsync_OutOfNodeScope_ReturnsEmpty()
    {
        var (sut, _, scope) = Build();
        var nodeId = Guid.NewGuid();
        var semId = Guid.NewGuid();
        scope.Setup(s => s.CanAccessStructureNodeAsync(nodeId, default)).ReturnsAsync(false);

        var result = await sut.GetForNodeSemesterAsync(nodeId, semId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForNodeSemesterAsync_OutOfSemesterScope_ReturnsEmpty()
    {
        var (sut, _, scope) = Build();
        var nodeId = Guid.NewGuid();
        var semId = Guid.NewGuid();
        scope.Setup(s => s.CanAccessStructureNodeAsync(nodeId, default)).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessSemesterAsync(semId, default)).ReturnsAsync(false);

        var result = await sut.GetForNodeSemesterAsync(nodeId, semId);

        result.Should().BeEmpty();
    }
}
