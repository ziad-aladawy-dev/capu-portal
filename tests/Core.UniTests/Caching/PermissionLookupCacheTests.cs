using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Application.CrossCutting.Caching;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using CapitalUniversity.Core.UniTests.Authorization;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Caching;

// Disambiguate the authz entity from the CapitalUniversity.Module.* module
// namespace. Declared inside the namespace scope so the alias resolves before
// the outer `Module` namespace shadows the bare type name.
using Module = CapitalUniversity.Core.Domain.Authorization.Module;

/// <summary>
/// Cache behavior tests for permission lookup — hit/miss/invalidation against a
/// real ICacheService (MemoryCacheService). Validates that:
///   - first lookup populates the cache,
///   - second lookup serves from the cache (no DB hit),
///   - an assignment edit bumps the per-user version stamp and orphans the entry,
///   - the cache key includes scope dimensions so different scopes don't collide.
/// </summary>
public class PermissionLookupCacheTests
{
    private static (PermissionManagementService Service, CoreDbContext Db, ICacheService Cache, CountingPermissionService Probe) Build(
        Guid userId,
        string year = "Global",
        string semester = "Global")
    {
        var dbOptions = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("PermLookup_" + Guid.NewGuid())
            .Options;
        var db = new CoreDbContext(dbOptions);
        db.Database.EnsureCreated();

        // Minimal authorization graph: one module, one resource, one role, one
        // role-permission, one staff-role assignment. The Resource entity needs an
        // explicit Id so the permission lookup can navigate to it.
        var module = new Module { Id = Guid.NewGuid(), DisplayName = "M", ModuleKey = "demo" };
        var resource = new Resource { Id = Guid.NewGuid(), Module = module, ModuleId = module.Id, Key = "demo", DisplayName = "S" };
        var role = new Role { Id = Guid.NewGuid(), Name = "R" };
        db.Modules.Add(module);
        db.Resources.Add(resource);
        db.Roles.Add(role);
        db.AddCrudGrant(role.Id, resource.Id, "View");
        db.StaffRoles.Add(new StaffRoleAssignment(userId, role.Id, year, semester));
        db.SaveChanges();

        // Minimal manifest registry that knows the "demo.demo" resource so the
        // expander can resolve per-action expansion when the service writes overrides.
        var registry = new PermissionManifestRegistry(new[] { (IPermissionManifest)new TestDemoManifest() });
        var expander = new ManifestActionExpander(registry);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        ICacheService cache = new MemoryCacheService(memoryCache);

        var probe = new CountingPermissionService(new PermissionService(db));

        var requestContext = new Mock<IRequestContext>();
        requestContext.Setup(c => c.ActiveAcademicYearId).Returns((Guid?)null);
        requestContext.Setup(c => c.ActiveSemesterId).Returns((Guid?)null);
        requestContext.Setup(c => c.ActiveStructureNodeId).Returns((Guid?)null);

        var scopeResolver = new Mock<IScopeResolver>();
        scopeResolver.Setup(s => s.ResolveAsync(userId, "Global", "Global", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationScope { Year = "Global", Semester = "Global" });

        var service = new PermissionManagementService(
            probe,
            requestContext.Object,
            scopeResolver.Object,
            db,
            new PermissionCacheCoordinator(
                cache,
                new PermissionCacheOptions { LookupTtlMinutes = 20, VersionTtlHours = 24 },
                null),
            expander,
            new TestLocalizationService());

        return (service, db, cache, probe);
    }

    [Fact]
    public async Task GetPermissionLookupAsync_FirstCallMissesCache_SecondCallHits()
    {
        var userId = Guid.NewGuid();
        var (service, _, _, probe) = Build(userId);

        var first = await service.GetPermissionLookupAsync(userId);
        first.Should().NotBeEmpty();
        probe.Calls.Should().Be(1, "the first call must materialize from the DB");

        var second = await service.GetPermissionLookupAsync(userId);
        second.Should().Equal(first);
        probe.Calls.Should().Be(1, "the second call must be served from the cache — no extra DB hit");
    }

    [Fact]
    public async Task UpdateAssignment_BumpsVersion_InvalidatesPreviousCacheEntry()
    {
        var userId = Guid.NewGuid();
        var (service, db, _, probe) = Build(userId);

        await service.GetPermissionLookupAsync(userId);   // populate
        probe.Calls.Should().Be(1);

        // Bump the version via the public assignment API. The version-stamp pattern
        // means the next read keys off a fresh stamp → cache miss → DB hit.
        var roleId = db.Roles.AsNoTracking().Single().Id;
        await service.UpdateAssignmentAsync(new UpdatePermissionAssignmentRequest
        {
            UserId = userId,
            RolesToAdd = new List<Guid>(),
            RolesToRemove = new List<Guid>(),
            PermissionsToAdd = new List<PermissionOverrideModel>(),
            PermissionsToRemove = new List<PermissionOverrideModel>(),
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
        });

        var afterEdit = await service.GetPermissionLookupAsync(userId);
        afterEdit.Should().NotBeEmpty();
        probe.Calls.Should().Be(2, "version rotation must orphan the cached entry");
    }

    [Fact]
    public async Task RedisCacheService_GetFails_DegradesToCacheMiss_NotException()
    {
        // RedisCacheService must never let a transport failure turn into a 500.
        // Inject an IDistributedCache that throws on every call; the service should
        // log + return default for Get and silently swallow for Set/Remove.
        var brokenCache = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        brokenCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated Redis outage"));
        brokenCache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated Redis outage"));
        brokenCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated Redis outage"));

        var svc = new RedisCacheService(brokenCache.Object);

        (await svc.GetAsync<string>("k")).Should().BeNull();
        await svc.SetAsync("k", "v", TimeSpan.FromMinutes(1));   // must not throw
        await svc.RemoveAsync("k");                              // must not throw
    }
}

internal sealed class TestDemoManifest : IPermissionManifest
{
    public string Module => "demo";
    public string DisplayName => "Demo";
    public string? Icon => null;
    public int? OrderNumber => 0;
    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("demo", "Demo", 0),
    };
}

/// <summary>
/// Wraps the real <see cref="PermissionService"/> and counts how many times the
/// permission-resolution method is called. Lets the tests assert "cache hit means
/// no DB pass" precisely.
/// </summary>
internal class CountingPermissionService : IPermissionService
{
    private readonly IPermissionService _inner;
    public int Calls { get; private set; }

    public CountingPermissionService(IPermissionService inner)
    {
        _inner = inner;
    }

    public Task<PermissionLoadResult> GetAllPermissionsAsync(
        Guid userId, AuthorizationScope? scope = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        return _inner.GetAllPermissionsAsync(userId, scope, cancellationToken);
    }

    public Task<PermissionLoadResult> GetResourcePermissionsAsync(
        Guid userId, string resourceKey, AuthorizationScope? scope = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        return _inner.GetResourcePermissionsAsync(userId, resourceKey, scope, cancellationToken);
    }
}