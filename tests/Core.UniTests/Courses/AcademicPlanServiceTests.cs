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
}
