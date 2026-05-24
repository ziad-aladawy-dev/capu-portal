using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.Courses;
using CapitalUniversity.Core.Application.Courses.Validators;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Courses;

/// <summary>
/// AcademicPlanService contract: validates inputs, composes plans by
/// referencing catalog courses (not duplicating them), and invalidates the
/// shared <c>academicplan:object:{id}</c> entry on every mutation —
/// including composition-level changes, since PlanCourses are part of the
/// plan's read model.
/// </summary>
public class AcademicPlanServiceTests
{
    private sealed class StubCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public int RemoveCalls;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    private static (AcademicPlanService Service, Mock<IAcademicPlanRepository> Plans, Mock<ICourseRepository> Courses, Mock<IUnitOfWork> Uow, StubCache Cache) Build()
    {
        var plans = new Mock<IAcademicPlanRepository>();
        var courses = new Mock<ICourseRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var scope = new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.IEffectiveScope>();
        scope.Setup(s => s.CanAccessStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validators = new AcademicPlanValidators(
            new CreateAcademicPlanValidator(),
            new UpdateAcademicPlanValidator(),
            new AddPlanCourseValidator());
        var service = new AcademicPlanService(
            uow.Object,
            plans.Object,
            courses.Object,
            validators,
            cache,
            scope.Object,
            new TestLocalizationService());
        return (service, plans, courses, uow, cache);
    }

    [Fact]
    public async Task Create_HappyPath_PersistsAndReturnsId()
    {
        var (sut, plans, _, uow, _) = Build();

        var id = await sut.CreateAsync(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "BSc CS 2025",
            EffectiveFrom = new DateTime(2025, 9, 1),
        });

        id.Should().NotBeEmpty();
        plans.Verify(r => r.AddAsync(It.IsAny<AcademicPlan>(), default), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Theory]
    [InlineData("",          "missing name")]
    [InlineData(null,        "null name")]
    public async Task Create_InvalidName_ThrowsValidation(string? name, string scenario)
    {
        var (sut, _, _, _, _) = Build();
        var act = () => sut.CreateAsync(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = name!,
            EffectiveFrom = new DateTime(2025, 9, 1),
        });
        await act.Should().ThrowAsync<ValidationException>(scenario);
    }

    [Fact]
    public async Task Create_EffectiveToBeforeFrom_ThrowsValidation()
    {
        var (sut, _, _, _, _) = Build();
        var act = () => sut.CreateAsync(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "Bad Plan",
            EffectiveFrom = new DateTime(2025, 9, 1),
            EffectiveTo = new DateTime(2024, 9, 1),
        });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetById_CacheMiss_HitsRepoAndCaches()
    {
        var (sut, plans, _, _, cache) = Build();
        var planId = Guid.NewGuid();
        plans.Setup(p => p.GetByIdAsync(planId, true, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId,
            Name = "X",
            EffectiveFrom = DateTime.UtcNow,
            StructureNodeId = Guid.NewGuid(),
        });

        var first = await sut.GetByIdAsync(planId);
        var second = await sut.GetByIdAsync(planId);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        plans.Verify(p => p.GetByIdAsync(planId, true, default), Times.Once);
    }

    [Fact]
    public async Task AddCourse_HappyPath_AppendsAndInvalidatesCache()
    {
        var (sut, plans, courses, uow, cache) = Build();
        var planId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var plan = new AcademicPlan { Id = planId, Name = "P", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid() };
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(plan);
        courses.Setup(c => c.GetByIdAsync(courseId, default)).ReturnsAsync(new Course { Id = courseId, Code = "CS101", Title = "T", CreditHours = 3 });
        plans.Setup(p => p.ContainsCourseAsync(planId, courseId, default)).ReturnsAsync(false);

        var entryId = await sut.AddCourseAsync(planId, new AddPlanCourseRequest
        {
            CourseId = courseId, Level = 1, Semester = 1, IsMandatory = true,
        });

        entryId.Should().NotBeEmpty();
        plan.PlanCourses.Should().ContainSingle(pc => pc.CourseId == courseId);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
        cache.RemoveCalls.Should().Be(1, "plan composition is part of the cached read model");
    }

    [Fact]
    public async Task AddCourse_DuplicateCourse_ThrowsConflict()
    {
        var (sut, plans, courses, _, _) = Build();
        var planId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan { Id = planId, Name = "P", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid() });
        courses.Setup(c => c.GetByIdAsync(courseId, default)).ReturnsAsync(new Course { Id = courseId, Code = "CS101", Title = "T", CreditHours = 3 });
        plans.Setup(p => p.ContainsCourseAsync(planId, courseId, default)).ReturnsAsync(true);

        var act = () => sut.AddCourseAsync(planId, new AddPlanCourseRequest
        {
            CourseId = courseId, Level = 1, Semester = 1,
        });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AddCourse_UnknownCourse_ThrowsNotFound()
    {
        var (sut, plans, courses, _, _) = Build();
        var planId = Guid.NewGuid();
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan { Id = planId, Name = "P", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid() });
        courses.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Course?)null);

        var act = () => sut.AddCourseAsync(planId, new AddPlanCourseRequest
        {
            CourseId = Guid.NewGuid(), Level = 1, Semester = 1,
        });
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveCourse_WrongPlan_ThrowsNotFound()
    {
        var (sut, plans, _, _, _) = Build();
        var planId = Guid.NewGuid();
        var planCourseId = Guid.NewGuid();
        plans.Setup(p => p.GetPlanCourseAsync(planCourseId, default)).ReturnsAsync(new AcademicPlanCourse
        {
            Id = planCourseId,
            AcademicPlanId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
        });

        var act = () => sut.RemoveCourseAsync(planId, planCourseId);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_InvalidatesCachedObject()
    {
        var (sut, plans, _, _, cache) = Build();
        var planId = Guid.NewGuid();
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "Old", EffectiveFrom = new DateTime(2025, 9, 1), StructureNodeId = Guid.NewGuid(),
        });

        await sut.UpdateAsync(planId, new UpdateAcademicPlanRequest { Name = "New" });

        cache.RemoveCalls.Should().Be(1);
    }

    // ============================================================
    // Task 2 mutation-resistance additions. Targets surviving
    // mutations from StrykerOutput/2026-05-19.23-25-17 on this
    // service: scope guards, cache-hit-with-stale-scope branch,
    // soft-update validation, delete/close/open paths, plan-course
    // wrong-plan guard, GetForStructureNode read.
    // ============================================================

    /// <summary>Helper that wires a service whose scope rejects the given node id.</summary>
    private static (AcademicPlanService Service, Mock<IAcademicPlanRepository> Plans, Mock<ICourseRepository> Courses, Mock<IUnitOfWork> Uow, StubCache Cache) BuildWithScopeRefusing(Guid blockedNodeId)
    {
        var plans = new Mock<IAcademicPlanRepository>();
        var courses = new Mock<ICourseRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var scope = new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.IEffectiveScope>();
        // Default allow; explicit deny for the requested node.
        scope.Setup(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessStructureNodeAsync(blockedNodeId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validators = new AcademicPlanValidators(
            new CreateAcademicPlanValidator(),
            new UpdateAcademicPlanValidator(),
            new AddPlanCourseValidator());
        var service = new AcademicPlanService(
            uow.Object, plans.Object, courses.Object, validators, cache, scope.Object, new TestLocalizationService());
        return (service, plans, courses, uow, cache);
    }

    [Fact]
    public async Task GetById_CacheHit_StaleScope_ReturnsNull()
    {
        // Cache contains a payload whose StructureNodeId the caller no longer
        // covers. The branch on line 81 must drop the cached row to null so a
        // shared cache cannot bypass scope after a role reassignment.
        var planId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, _, _, cache) = BuildWithScopeRefusing(blockedNode);
        await cache.SetAsync($"academicplan:object:{planId:N}",
            new AcademicPlanResponse { Id = planId, Name = "Cached", StructureNodeId = blockedNode });

        var result = await sut.GetByIdAsync(planId);

        result.Should().BeNull("the cached projection must be scope-checked on every read");
        plans.Verify(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never,
            "scope-rejected cache hits must NOT fall through to the repo");
    }

    [Fact]
    public async Task GetById_CacheMiss_RepoReturnsNull_NoCacheWrite()
    {
        var planId = Guid.NewGuid();
        var (sut, plans, _, _, cache) = Build();
        plans.Setup(p => p.GetByIdAsync(planId, true, default)).ReturnsAsync((AcademicPlan?)null);

        var result = await sut.GetByIdAsync(planId);

        result.Should().BeNull();
        var cached = await cache.GetAsync<AcademicPlanResponse>($"academicplan:object:{planId:N}");
        cached.Should().BeNull("a null repo result must not be cached as a positive hit");
    }

    [Fact]
    public async Task GetById_CacheMiss_RepoFound_OutOfScope_ReturnsNullAndDoesNotCache()
    {
        var planId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, _, _, cache) = BuildWithScopeRefusing(blockedNode);
        plans.Setup(p => p.GetByIdAsync(planId, true, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = blockedNode,
        });

        var result = await sut.GetByIdAsync(planId);

        result.Should().BeNull();
        var cached = await cache.GetAsync<AcademicPlanResponse>($"academicplan:object:{planId:N}");
        cached.Should().BeNull("an out-of-scope row must not warm the cache for the next caller");
    }

    [Fact]
    public async Task GetForStructureNode_OutOfScope_ReturnsEmpty_DoesNotHitRepo()
    {
        var node = Guid.NewGuid();
        var (sut, plans, _, _, _) = BuildWithScopeRefusing(node);

        var list = await sut.GetForStructureNodeAsync(node);

        list.Should().BeEmpty();
        plans.Verify(p => p.GetForStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForStructureNode_InScope_ReturnsRepoRows_Localized()
    {
        var node = Guid.NewGuid();
        var (sut, plans, _, _, _) = Build();
        plans.Setup(p => p.GetForStructureNodeAsync(node, default)).ReturnsAsync(new List<AcademicPlan>
        {
            new() { Id = Guid.NewGuid(), Name = "A", EffectiveFrom = DateTime.UtcNow, StructureNodeId = node },
            new() { Id = Guid.NewGuid(), Name = "B", EffectiveFrom = DateTime.UtcNow, StructureNodeId = node },
        });

        var list = await sut.GetForStructureNodeAsync(node);

        list.Should().HaveCount(2);
        list.Select(p => p.StructureNodeId).Should().AllBeEquivalentTo(node);
    }

    [Fact]
    public async Task Create_OutOfScope_ThrowsNotFound()
    {
        var node = Guid.NewGuid();
        var (sut, plans, _, uow, _) = BuildWithScopeRefusing(node);

        var act = () => sut.CreateAsync(new CreateAcademicPlanRequest
        {
            StructureNodeId = node, Name = "P", EffectiveFrom = new DateTime(2025, 9, 1),
        });

        await act.Should().ThrowAsync<NotFoundException>("creation against a node the caller can't see must be indistinguishable from absence");
        plans.Verify(p => p.AddAsync(It.IsAny<AcademicPlan>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_PlanLoadedButOutOfScope_ThrowsNotFound()
    {
        var planId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, _, _, cache) = BuildWithScopeRefusing(blockedNode);
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = blockedNode,
        });

        var act = () => sut.UpdateAsync(planId, new UpdateAcademicPlanRequest { Name = "Y" });

        await act.Should().ThrowAsync<NotFoundException>();
        cache.RemoveCalls.Should().Be(0, "no mutation occurred — cache must NOT be invalidated");
    }

    [Fact]
    public async Task Update_EffectiveToEqualToFrom_ThrowsValidation()
    {
        // The guard is `<=`, so equal endpoints are invalid too. Catches a
        // mutation flipping `<=` to `<`.
        var (sut, plans, _, _, _) = Build();
        var planId = Guid.NewGuid();
        var when = new DateTime(2025, 9, 1);
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = when, StructureNodeId = Guid.NewGuid(),
        });

        var act = () => sut.UpdateAsync(planId, new UpdateAcademicPlanRequest { EffectiveTo = when });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_NullName_DoesNotOverwriteExistingName()
    {
        // Pinning the `if (request.Name != null)` guard. A mutation flipping
        // this to `if (request.Name == null)` would overwrite the existing
        // name with null, which is exactly the bug a sparse-PATCH must avoid.
        var (sut, plans, _, _, _) = Build();
        var planId = Guid.NewGuid();
        var preserved = "Preserved";
        var plan = new AcademicPlan
        {
            Id = planId, Name = preserved, EffectiveFrom = new DateTime(2025, 9, 1), StructureNodeId = Guid.NewGuid(),
        };
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(plan);

        await sut.UpdateAsync(planId, new UpdateAcademicPlanRequest { /* Name = null */ });

        plan.Name.Should().Be(preserved);
    }

    [Fact]
    public async Task Delete_HappyPath_RemovesAndInvalidatesCache()
    {
        var planId = Guid.NewGuid();
        var (sut, plans, _, uow, cache) = Build();
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid(),
        });

        await sut.DeleteAsync(planId);

        plans.Verify(p => p.Delete(It.IsAny<AcademicPlan>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Delete_MissingPlan_ThrowsNotFound_AndDoesNotInvalidateCache()
    {
        var planId = Guid.NewGuid();
        var (sut, plans, _, _, cache) = Build();
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync((AcademicPlan?)null);

        var act = () => sut.DeleteAsync(planId);

        await act.Should().ThrowAsync<NotFoundException>();
        cache.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Delete_OutOfScope_ThrowsNotFound()
    {
        var planId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, _, _, _) = BuildWithScopeRefusing(blockedNode);
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = blockedNode,
        });

        var act = () => sut.DeleteAsync(planId);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddCourse_OutOfScope_ThrowsNotFound_DoesNotTouchCourseRepo()
    {
        var planId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, courses, _, _) = BuildWithScopeRefusing(blockedNode);
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = blockedNode,
        });

        var act = () => sut.AddCourseAsync(planId, new AddPlanCourseRequest { CourseId = Guid.NewGuid(), Level = 1, Semester = 1 });

        await act.Should().ThrowAsync<NotFoundException>();
        courses.Verify(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "scope rejection must short-circuit before any catalog lookup");
    }

    [Fact]
    public async Task RemoveCourse_HappyPath_RemovesAndInvalidatesCache()
    {
        var planId = Guid.NewGuid();
        var planCourseId = Guid.NewGuid();
        var (sut, plans, _, uow, cache) = Build();
        plans.Setup(p => p.GetPlanCourseAsync(planCourseId, default)).ReturnsAsync(new AcademicPlanCourse
        {
            Id = planCourseId, AcademicPlanId = planId, CourseId = Guid.NewGuid(),
        });
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid(),
        });

        await sut.RemoveCourseAsync(planId, planCourseId);

        plans.Verify(p => p.RemovePlanCourse(It.Is<AcademicPlanCourse>(e => e.Id == planCourseId)), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task RemoveCourse_OutOfScope_ThrowsNotFound()
    {
        var planId = Guid.NewGuid();
        var planCourseId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, _, _, _) = BuildWithScopeRefusing(blockedNode);
        plans.Setup(p => p.GetPlanCourseAsync(planCourseId, default)).ReturnsAsync(new AcademicPlanCourse
        {
            Id = planCourseId, AcademicPlanId = planId, CourseId = Guid.NewGuid(),
        });
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = blockedNode,
        });

        var act = () => sut.RemoveCourseAsync(planId, planCourseId);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CloseRecord_HappyPath_ClosesAndInvalidatesCache()
    {
        var planId = Guid.NewGuid();
        var (sut, plans, _, uow, cache) = Build();
        var plan = new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid(),
        };
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(plan);

        await sut.CloseRecordAsync(planId);

        plan.IsClosed.Should().BeTrue();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task OpenRecord_AfterClose_RestoresIsClosedFalse()
    {
        var planId = Guid.NewGuid();
        var (sut, plans, _, _, cache) = Build();
        var plan = new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = Guid.NewGuid(),
        };
        plan.Close();
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(plan);

        await sut.OpenRecordAsync(planId);

        plan.IsClosed.Should().BeFalse();
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task CloseRecord_OutOfScope_ThrowsNotFound_NoCacheRemove()
    {
        var planId = Guid.NewGuid();
        var blockedNode = Guid.NewGuid();
        var (sut, plans, _, _, cache) = BuildWithScopeRefusing(blockedNode);
        plans.Setup(p => p.GetByIdAsync(planId, false, default)).ReturnsAsync(new AcademicPlan
        {
            Id = planId, Name = "X", EffectiveFrom = DateTime.UtcNow, StructureNodeId = blockedNode,
        });

        var act = () => sut.CloseRecordAsync(planId);

        await act.Should().ThrowAsync<NotFoundException>();
        cache.RemoveCalls.Should().Be(0);
    }
}
