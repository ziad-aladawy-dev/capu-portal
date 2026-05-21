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
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.CourseOfferings;

/// <summary>
/// CourseOfferingService contract: scope gates every read/write; section
/// uniqueness rejected at create + on section-code update; out-of-scope
/// reads silently return null (no existence leak).
/// </summary>
public class CourseOfferingServiceTests
{
    private static (CourseOfferingService Service, Mock<ICourseOfferingRepository> Repo, Mock<IUnitOfWork> Uow, Mock<IEffectiveScope> Scope) Build(bool inScope = true)
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
        return (sut, repo, uow, scope);
    }

    private static CreateCourseOfferingRequest ValidCreate() => new()
    {
        CourseId = Guid.NewGuid(),
        SemesterId = Guid.NewGuid(),
        StructureNodeId = Guid.NewGuid(),
        SectionCode = "A",
        Capacity = 30,
    };

    [Fact]
    public async Task Create_HappyPath_PersistsAndReturnsId()
    {
        var (sut, repo, uow, _) = Build();
        repo.Setup(r => r.SectionExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(false);
        CourseOfferingEntity? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<CourseOfferingEntity>(), default))
            .Callback<CourseOfferingEntity, CancellationToken>((o, _) => captured = o);

        var id = await sut.CreateAsync(ValidCreate());

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.Capacity.Should().Be(30);
        captured.RegisteredCount.Should().Be(0);
        captured.Status.Should().Be(OfferingStatus.Draft);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_DuplicateSection_ThrowsConflict()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.SectionExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate());
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_OutOfScope_ThrowsNotFound()
    {
        var (sut, _, _, _) = Build(inScope: false);

        var act = () => sut.CreateAsync(ValidCreate());
        // Out-of-scope is reported as NotFound to avoid leaking node existence.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_NegativeCapacity_ThrowsValidation()
    {
        var (sut, _, _, _) = Build();
        var req = ValidCreate();
        req.Capacity = -1;

        var act = () => sut.CreateAsync(req);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_SectionCollision_ThrowsConflict()
    {
        var (sut, repo, _, _) = Build();
        var existing = NewOffering(sectionCode: "A");
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);
        repo.Setup(r => r.SectionExistsAsync(existing.CourseId, existing.SemesterId, existing.StructureNodeId, "B", default))
            .ReturnsAsync(true);

        var act = () => sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { SectionCode = "B" });
        await act.Should().ThrowAsync<ConflictException>();
        existing.SectionCode.Should().Be("A", "collision must reject the rename rather than partially apply it");
    }

    [Fact]
    public async Task Update_AppliesOnlyProvidedFields()
    {
        var (sut, repo, uow, _) = Build();
        // NewOffering() defaults to Status=Draft, RegistrationState=Closed.
        var existing = NewOffering(sectionCode: "A");
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest
        {
            Status = OfferingStatus.Open,
            // RegistrationState left null — must stay Closed.
        });

        existing.Status.Should().Be(OfferingStatus.Open);
        existing.RegistrationState.Should().Be(RegistrationState.Closed);
        existing.UpdatedAt.Should().NotBeNull();
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Update_CapacityBelowRegistered_ThrowsConflict()
    {
        var (sut, repo, _, _) = Build();
        var existing = NewOffering(capacity: 5);
        existing.IncrementRegistration();
        existing.IncrementRegistration();
        existing.IncrementRegistration();
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var act = () => sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Capacity = 2 });
        await act.Should().ThrowAsync<ConflictException>();
        existing.Capacity.Should().Be(5);
    }

    [Fact]
    public async Task Update_OutOfScope_ThrowsNotFound()
    {
        var existing = NewOffering();

        var (sut, repo, _, _) = Build(inScope: false);
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var act = () => sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Status = OfferingStatus.Open });
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetById_OutOfScope_ReturnsNull()
    {
        var existing = NewOffering();

        var (sut, repo, _, _) = Build(inScope: false);
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var result = await sut.GetByIdAsync(existing.Id);
        result.Should().BeNull();
    }

    private static CourseOfferingEntity NewOffering(int capacity = 30, string sectionCode = "A")
    {
        var entity = new CourseOfferingEntity
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = sectionCode,
        };
        entity.InitializeCapacity(capacity);
        return entity;
    }
}
