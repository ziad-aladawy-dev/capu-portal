using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.Courses;
using CapitalUniversity.Core.Application.Courses.Validators;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Courses;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Courses;

/// <summary>
/// CourseService contract: validates inputs, enforces unique catalog code,
/// reads-through cache for object lookups, and invalidates the cache entry
/// on every mutation per the shared-object-cache pattern in
/// <c>docs/caching-strategy.md</c>.
/// </summary>
public class CourseServiceTests
{
    private sealed class StubCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public int SetCalls;
        public int RemoveCalls;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        {
            SetCalls++;
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

    private static (CourseService Service, Mock<ICourseRepository> Repo, Mock<IUnitOfWork> Uow, StubCache Cache) Build()
    {
        var repo = new Mock<ICourseRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var service = new CourseService(
            uow.Object,
            repo.Object,
            new CreateCourseValidator(),
            new UpdateCourseValidator(),
            cache);
        return (service, repo, uow, cache);
    }

    [Fact]
    public async Task Create_HappyPath_PersistsAndReturnsId()
    {
        var (sut, repo, uow, _) = Build();
        repo.Setup(r => r.CodeExistsAsync("CS101", null, default)).ReturnsAsync(false);

        var id = await sut.CreateAsync(new CreateCourseRequest
        {
            Code = "CS101",
            Title = "Algorithms",
            CreditHours = 3,
            Category = CourseCategory.ProgramRequirement,
        });

        id.Should().NotBeEmpty();
        repo.Verify(r => r.AddAsync(It.Is<Course>(c => c.Code == "CS101"), default), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_DuplicateCode_ThrowsConflict()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.CodeExistsAsync("CS101", null, default)).ReturnsAsync(true);

        var act = () => sut.CreateAsync(new CreateCourseRequest
        {
            Code = "CS101",
            Title = "Algorithms",
            CreditHours = 3,
        });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData("",     "Title", 3,  "code required")]
    [InlineData("CS",   "",      3,  "title required")]
    [InlineData("CS",   "Title", 13, "credit hours out of range")]
    [InlineData("CS",   "Title", -1, "negative credit hours")]
    public async Task Create_InvalidInput_ThrowsValidation(string code, string title, int hours, string scenario)
    {
        var (sut, _, _, _) = Build();
        var act = () => sut.CreateAsync(new CreateCourseRequest
        {
            Code = code,
            Title = title,
            CreditHours = hours,
        });

        await act.Should().ThrowAsync<ValidationException>(scenario);
    }

    [Fact]
    public async Task GetById_CacheMiss_HitsRepoAndCachesResult()
    {
        var (sut, repo, _, cache) = Build();
        var courseId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(courseId, default)).ReturnsAsync(new Course
        {
            Id = courseId, Code = "CS101", Title = "T", CreditHours = 3, IsActive = true,
        });

        var result = await sut.GetByIdAsync(courseId);

        result.Should().NotBeNull();
        cache.SetCalls.Should().Be(1, "cache miss should populate the shared object key");
        repo.Verify(r => r.GetByIdAsync(courseId, default), Times.Once);
    }

    [Fact]
    public async Task GetById_CacheHit_DoesNotHitRepository()
    {
        var (sut, repo, _, _) = Build();
        var courseId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(courseId, default)).ReturnsAsync(new Course
        {
            Id = courseId, Code = "CS101", Title = "T", CreditHours = 3, IsActive = true,
        });

        await sut.GetByIdAsync(courseId);
        await sut.GetByIdAsync(courseId);

        repo.Verify(r => r.GetByIdAsync(courseId, default), Times.Once,
            "second lookup must be served from cache");
    }

    [Fact]
    public async Task Update_InvalidatesCachedObject()
    {
        var (sut, repo, _, cache) = Build();
        var courseId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(courseId, default)).ReturnsAsync(new Course
        {
            Id = courseId, Code = "CS101", Title = "T", CreditHours = 3, IsActive = true,
        });

        // Prime the cache.
        await sut.GetByIdAsync(courseId);
        cache.RemoveCalls.Should().Be(0);

        await sut.UpdateAsync(courseId, new UpdateCourseRequest { Title = "Updated", CreditHours = 4 });

        cache.RemoveCalls.Should().Be(1, "mutations must invalidate the shared object payload");
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Course?)null);

        var act = () => sut.UpdateAsync(Guid.NewGuid(), new UpdateCourseRequest { Title = "X" });
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_RemovesAndDropsCacheEntry()
    {
        var (sut, repo, uow, cache) = Build();
        var courseId = Guid.NewGuid();
        var entity = new Course { Id = courseId, Code = "CS101", Title = "T", CreditHours = 3 };
        repo.Setup(r => r.GetByIdAsync(courseId, default)).ReturnsAsync(entity);

        await sut.DeleteAsync(courseId);

        repo.Verify(r => r.Delete(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
        cache.RemoveCalls.Should().Be(1);
    }
}
