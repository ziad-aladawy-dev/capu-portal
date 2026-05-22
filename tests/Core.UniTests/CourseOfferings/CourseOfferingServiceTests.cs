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
        // Pins that the service actually marks the entity for update.
        // Without this assertion, a mutation that drops `_offerings.Update(...)`
        // from the service body still saves the (tracked) entity in production
        // EF — but the explicit Update() call is the contract callers rely on
        // for the repository abstraction (untracked entities, sandboxed
        // contexts, etc.). Mutation L143 survived without this check.
        repo.Verify(r => r.Update(existing), Times.Once);
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

    // ----------------------------------------------------------------------
    // Edge-case tests targeting specific mutation survivors on the service.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetById_RepoReturnsNull_ReturnsNullWithoutScopeCheck()
    {
        // Pins the "offering is null" early-return guard in GetByIdAsync. A
        // mutation that flips it to "is not null" would skip the return and
        // call _scope.CanAccessStructureNodeAsync on a null entity, throwing
        // NullReferenceException. Verify both the null result AND that scope
        // was never consulted (no entity = no node to check).
        var repo = new Mock<ICourseOfferingRepository>();
        var uow = new Mock<IUnitOfWork>();
        var scope = new Mock<IEffectiveScope>();
        scope.Setup(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((CourseOfferingEntity?)null);

        var sut = new CourseOfferingService(
            uow.Object, repo.Object,
            new CreateCourseOfferingValidator(),
            new UpdateCourseOfferingValidator(),
            scope.Object);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
        scope.Verify(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "scope must not be queried when there's no offering to gate");
    }

    [Fact]
    public async Task GetForNodeSemester_OutOfScope_ReturnsEmpty_DoesNotQueryRepo()
    {
        // Pins the early-return on out-of-scope reads. A mutation that
        // removes the return short-circuits to the repo call which would
        // leak rows the caller cannot see.
        var (sut, repo, _, _) = Build(inScope: false);

        var result = await sut.GetForNodeSemesterAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeEmpty();
        repo.Verify(
            r => r.GetForNodeSemesterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<OfferingStatus?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "out-of-scope reads must short-circuit BEFORE the repo call");
    }

    [Fact]
    public async Task Create_PersistsFieldsFromRequest()
    {
        // Pins the object-initializer mutation: if the initializer body is
        // emptied, the persisted entity loses CourseId, SemesterId, etc.
        var (sut, repo, uow, _) = Build();
        repo.Setup(r => r.SectionExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(false);
        CourseOfferingEntity? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<CourseOfferingEntity>(), default))
            .Callback<CourseOfferingEntity, CancellationToken>((o, _) => captured = o);

        var req = new CreateCourseOfferingRequest
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "EVE-7",
            Capacity = 42,
            Status = OfferingStatus.Open,
            RegistrationState = RegistrationState.Open,
            ExternalSystemId = "ext-123",
        };

        await sut.CreateAsync(req);

        captured.Should().NotBeNull();
        captured!.CourseId.Should().Be(req.CourseId);
        captured.SemesterId.Should().Be(req.SemesterId);
        captured.StructureNodeId.Should().Be(req.StructureNodeId);
        captured.SectionCode.Should().Be("EVE-7");
        captured.Capacity.Should().Be(42);
        captured.Status.Should().Be(OfferingStatus.Open);
        captured.RegistrationState.Should().Be(RegistrationState.Open);
        captured.ExternalSystemId.Should().Be("ext-123");
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Update_SectionCodeSameAsCurrent_SkipsSectionExistsCheck()
    {
        // Pins the AND→OR logical mutation on ApplySectionCodeAsync's guard.
        // The guard short-circuits when SectionCode is null OR unchanged.
        // If mutated to OR, a same-value section would trigger an unnecessary
        // SectionExists query (and potentially conflict against itself if the
        // index hit on the current row).
        var (sut, repo, _, _) = Build();
        var existing = NewOffering(sectionCode: "A");
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest
        {
            SectionCode = "A", // same as current
        });

        repo.Verify(
            r => r.SectionExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "same-section no-op must NOT hit the section-exists index");
        existing.SectionCode.Should().Be("A");
    }

    [Fact]
    public async Task Update_ExternalSyncedAtProvided_PersistsValue()
    {
        // Pins the "ExternalSyncedAt.HasValue" guard in ApplyExternalSyncMetadata.
        // Without the test, a mutation that negates the HasValue check would
        // either always-apply or never-apply, both surviving silently.
        var (sut, repo, _, _) = Build();
        var existing = NewOffering();
        existing.ExternalSyncedAt = null;
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        var syncedAt = new DateTime(2026, 5, 21, 14, 0, 0, DateTimeKind.Utc);
        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { ExternalSyncedAt = syncedAt });

        existing.ExternalSyncedAt.Should().Be(syncedAt);
    }

    [Fact]
    public async Task Update_ExternalSyncedAtOmitted_LeavesPreviousValueIntact()
    {
        // Symmetry with the previous test: omitting the field must NOT
        // overwrite the persisted value. Without this, a mutation that
        // unconditionally assigns null would slip through.
        var (sut, repo, _, _) = Build();
        var existing = NewOffering();
        var original = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        existing.ExternalSyncedAt = original;
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { Capacity = 50 });

        existing.ExternalSyncedAt.Should().Be(original);
    }

    [Fact]
    public async Task Update_ExternalSystemIdProvided_PersistsValue()
    {
        var (sut, repo, _, _) = Build();
        var existing = NewOffering();
        existing.ExternalSystemId = null;
        repo.Setup(r => r.GetByIdAsync(existing.Id, default)).ReturnsAsync(existing);

        await sut.UpdateAsync(existing.Id, new UpdateCourseOfferingRequest { ExternalSystemId = "ext-9" });

        existing.ExternalSystemId.Should().Be("ext-9");
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
