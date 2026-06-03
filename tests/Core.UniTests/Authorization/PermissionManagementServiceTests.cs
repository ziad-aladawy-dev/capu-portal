using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.UniTests._Helpers;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class PermissionManagementServiceTests
{
    private static CoreDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var ctx = new CoreDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static PermissionManagementService CreateService(
        CoreDbContext dbContext,
        Guid? overrideResourceId,
        out Mock<IPermissionService> mockPermissionService,
        out Mock<IRequestContext> mockRequestContext,
        out Mock<IScopeResolver> mockScopeResolver,
        out Mock<ICacheService> mockCache,
        out Mock<ICurrentUser> mockCurrentUser)
    {
        mockPermissionService = new Mock<IPermissionService>();
        mockRequestContext = new Mock<IRequestContext>();
        mockScopeResolver = new Mock<IScopeResolver>();
        mockCache = new Mock<ICacheService>();
        mockCurrentUser = new Mock<ICurrentUser>();

        var registry = new PermissionManifestRegistry(new[] { (IPermissionManifest)new TestPermissionsManifest() });
        var expander = new ManifestActionExpander(registry);

        // Seed a Module + Resource so the management service can resolve the
        // module key when writing per-action override rows.
        if (overrideResourceId.HasValue && !dbContext.Resources.Any(r => r.Id == overrideResourceId.Value))
        {
            var module = new Module
            {
                Id = Guid.NewGuid(),
                ModuleKey = "permissions",
                DisplayName = "Permissions",
            };
            dbContext.Modules.Add(module);
            dbContext.Resources.Add(new Resource
            {
                Id = overrideResourceId.Value,
                ModuleId = module.Id,
                Key = "permissions",
                DisplayName = "Permissions",
            });
            dbContext.SaveChanges();
        }

        return new PermissionManagementService(
            mockPermissionService.Object,
            mockRequestContext.Object,
            mockScopeResolver.Object,
            dbContext,
            new PermissionCacheCoordinator(mockCache.Object, new PermissionCacheOptions(), null),
            expander,
            new TestLocalizationService());
    }

    [Fact]
    public async Task CreateAssignmentAsync_ValidRequest_CreatesRolesAndPerActionOverrides()
    {
        // Arrange
        var dbContext = GetDbContext();
        var resourceId = Guid.NewGuid();
        var service = CreateService(dbContext, resourceId, out _, out _, out _, out _, out _);

        var request = new CreatePermissionAssignmentRequest
        {
            UserId = Guid.NewGuid(),
            RoleIds = new List<Guid> { Guid.NewGuid() },
            PermissionOverrides = new List<PermissionOverrideModel>
            {
                new PermissionOverrideModel
                {
                    ResourceId = resourceId,
                    Actions = new List<string> { "EditClose" },
                    Type = OverrideType.Allow
                }
            },
            StructuralScope = new StructuralScopeModel { StructureNodeId = Guid.NewGuid() },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true }
        };

        // Act
        var result = await service.CreateAssignmentAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.UserId, result.UserId);
        Assert.Single(dbContext.StaffRoles);

        // EditClose implies View+Insert+EditClose on the canonical CRUD ladder,
        // so writing one override DTO produces three per-action rows.
        var actions = dbContext.StaffPermissions
            .Where(sp => sp.ResourceId == resourceId)
            .Select(sp => sp.Action)
            .ToList();
        Assert.Equal(3, actions.Count);
        Assert.Contains("View", actions);
        Assert.Contains("Insert", actions);
        Assert.Contains("EditClose", actions);
    }

    [Fact]
    public async Task GetAssignmentAsync_WhenExists_ReturnsAssignment()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = CreateService(dbContext, null, out _, out _, out _, out _, out _);

        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();

        dbContext.StaffRoles.Add(new StaffRoleAssignment(userId, Guid.NewGuid(), yearId.ToString(), semesterId.ToString())
        {
            StructureNodeId = nodeId
        });
        await dbContext.SaveChangesAsync();

        var query = new GetPermissionAssignmentQueryDto
        {
            UserId = userId,
            StructureNodeId = nodeId,
            AcademicYearId = yearId,
            SemesterId = semesterId
        };

        // Act
        var result = await service.GetAssignmentAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(nodeId, result.StructuralScope.StructureNodeId);
        Assert.Equal(yearId, result.TemporalScope.AcademicYearId);
    }

    [Fact]
    public async Task UpdateAssignmentAsync_RemovesAndAddsCorrectly()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = CreateService(dbContext, null, out _, out _, out _, out _, out _);

        var userId = Guid.NewGuid();
        var role1 = Guid.NewGuid();
        var role2 = Guid.NewGuid();

        dbContext.StaffRoles.Add(new StaffRoleAssignment(userId, role1, "Global", "Global"));
        await dbContext.SaveChangesAsync();

        var request = new UpdatePermissionAssignmentRequest
        {
            UserId = userId,
            RolesToRemove = new List<Guid> { role1 },
            RolesToAdd = new List<Guid> { role2 },
            PermissionsToRemove = new List<PermissionOverrideModel>(),
            PermissionsToAdd = new List<PermissionOverrideModel>(),
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AlwaysActive = true }
        };

        // Act
        await service.UpdateAssignmentAsync(request);

        // Assert
        var currentRoles = await dbContext.StaffRoles.Where(r => r.StaffId == userId).ToListAsync();
        Assert.Single(currentRoles);
        Assert.Equal(role2, currentRoles.First().RoleId);
    }

    // ---------------------------------------------------------------------
    // Re-scoping a single permission + returning it, the edge cases around
    // scope-keyed overrides, and the manually-triggered ExpiresAt sweep.
    // These drive GetEffectivePermissionsAsync, which reads through the cache,
    // so they use a pass-through ICacheService whose null GetAsync lets the
    // default GetOrSetAsync run the rebuild factory against the in-memory DB.
    // ---------------------------------------------------------------------

    private sealed class PassThroughCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        // GetOrSetAsync intentionally left to the interface default: GetAsync
        // returns null, so the factory always runs (no caching between calls).
    }

    private static Guid SeedPermissionsResource(CoreDbContext db)
    {
        var moduleId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        db.Modules.Add(new Module { Id = moduleId, ModuleKey = "permissions", DisplayName = "Permissions" });
        db.Resources.Add(new Resource { Id = resourceId, ModuleId = moduleId, Key = "permissions", DisplayName = "Permissions" });
        db.SaveChanges();
        return resourceId;
    }

    private static PermissionManagementService CreateServiceWithCache(CoreDbContext dbContext, ICacheService cache)
    {
        var registry = new PermissionManifestRegistry(new[] { (IPermissionManifest)new TestPermissionsManifest() });
        var expander = new ManifestActionExpander(registry);

        return new PermissionManagementService(
            new Mock<IPermissionService>().Object,
            new Mock<IRequestContext>().Object,
            new Mock<IScopeResolver>().Object,
            dbContext,
            new PermissionCacheCoordinator(cache, new PermissionCacheOptions(), null),
            expander,
            new TestLocalizationService());
    }

    private static PermissionOverrideModel AllowAction(Guid resourceId, string action) =>
        new() { ResourceId = resourceId, Type = OverrideType.Allow, Actions = new List<string> { action } };

    private static PermissionOverrideModel DenyAction(Guid resourceId, string action) =>
        new() { ResourceId = resourceId, Type = OverrideType.Deny, Actions = new List<string> { action } };

    [Fact]
    public async Task GetEffectivePermissions_RescopeOnePermission_AddsSecondTuple_ThenReturnReverts()
    {
        // Arrange: a role grants View, assigned at structural scope X (global temporal).
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();
        var nodeY = Guid.NewGuid();

        db.RolePermissions.Add(new RolePermission(roleId, resourceId, "View"));
        db.StaffRoles.Add(new StaffRoleAssignment(userId, roleId, "Global", "Global") { StructureNodeId = nodeX });
        await db.SaveChangesAsync();

        // Baseline: exactly one View tuple, scoped to node X.
        var baseline = (await service.GetEffectivePermissionsAsync(userId)).Where(p => p.Action == "View").ToList();
        Assert.Single(baseline);
        Assert.Equal(nodeX, baseline[0].Scope.StructureNodeId);

        // Act 1 — re-scope that one permission to a DIFFERENT structural scope (node Y)
        // via an Allow override. Scope is identity, so this is additive, not a move.
        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeY },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        // Assert: the same permission now surfaces as TWO tuples, one per scope.
        var rescoped = (await service.GetEffectivePermissionsAsync(userId)).Where(p => p.Action == "View").ToList();
        Assert.Equal(2, rescoped.Count);
        Assert.Contains(rescoped, p => p.Scope.StructureNodeId == nodeX);
        Assert.Contains(rescoped, p => p.Scope.StructureNodeId == nodeY);

        // Act 2 — return it: remove the divergent override at its EXACT scope (Y).
        await service.UpdateAssignmentAsync(new UpdatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeY },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionsToRemove = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        // Assert: collapsed back to the single role-scoped tuple at node X.
        var reverted = (await service.GetEffectivePermissionsAsync(userId)).Where(p => p.Action == "View").ToList();
        Assert.Single(reverted);
        Assert.Equal(nodeX, reverted[0].Scope.StructureNodeId);
    }

    [Fact]
    public async Task ReturnOverride_AtWrongScope_LeavesDivergentRowIntact()
    {
        // Edge case: removals must target the exact scope tuple. A remove aimed at a
        // different scope matches nothing and silently leaves the override in place.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();
        var nodeY = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeY },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        // Try to remove at scope X — a different tuple from where the row lives (Y).
        await service.UpdateAssignmentAsync(new UpdatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeX },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionsToRemove = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        var rows = db.StaffPermissions.ToList();
        Assert.Single(rows);
        Assert.Equal(nodeY, rows[0].StructureNodeId);
    }

    [Fact]
    public async Task ReturnOverride_WithoutRoleFallback_RemovesPermissionEntirely()
    {
        // Edge case: when a permission exists ONLY via the override (the role never
        // granted it), "returning" it deletes it outright — there is nothing to fall
        // back to.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var nodeY = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeY },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });
        Assert.Single(await service.GetEffectivePermissionsAsync(userId), p => p.Action == "View");

        await service.UpdateAssignmentAsync(new UpdatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeY },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionsToRemove = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        Assert.Empty(await service.GetEffectivePermissionsAsync(userId));
        Assert.Empty(db.StaffPermissions);
    }

    [Fact]
    public async Task AllowOverride_DuplicatingRoleGrant_StillWritesRedundantRow()
    {
        // Edge case: dedup runs only among overrides, never against role grants, so an
        // Allow override that mirrors what the role already grants is still persisted.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();

        db.RolePermissions.Add(new RolePermission(roleId, resourceId, "View"));
        db.StaffRoles.Add(new StaffRoleAssignment(userId, roleId, "Global", "Global") { StructureNodeId = nodeX });
        await db.SaveChangesAsync();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeX },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        Assert.Single(db.StaffPermissions.Where(sp => sp.Action == "View" && sp.Type == OverrideType.Allow));
    }

    [Fact]
    public async Task DenyOverride_OnRoleGrantedAction_SameScope_WinsOverGrant()
    {
        // Edge case: a Deny carve-out at the role's own scope removes that action from
        // the effective set (allow − deny), while sibling actions survive.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();

        db.RolePermissions.Add(new RolePermission(roleId, resourceId, "View"));
        db.RolePermissions.Add(new RolePermission(roleId, resourceId, "Delete"));
        db.StaffRoles.Add(new StaffRoleAssignment(userId, roleId, "Global", "Global") { StructureNodeId = nodeX });
        // Deny Delete at the SAME scope key as the role assignment.
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "Delete", OverrideType.Deny, "Global", "Global") { StructureNodeId = nodeX });
        await db.SaveChangesAsync();

        var eff = await service.GetEffectivePermissionsAsync(userId);
        Assert.Contains(eff, p => p.Action == "View");
        Assert.DoesNotContain(eff, p => p.Action == "Delete");
    }

    [Fact]
    public async Task PersistOverride_OppositeTypeSameScope_TogglesToDelete()
    {
        // Edge case: applying the opposite type at the same scope removes the stored
        // rows (toggle-to-default) rather than inserting conflicting ones.
        // Allow ["Delete"] expands (forward) to the full ladder = 5 rows; Deny ["View"]
        // expands (reverse) to every verb that grants View = the same 5 actions, so each
        // toggles its Allow counterpart off, leaving nothing.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeX },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "Delete") }
        });
        Assert.Equal(5, db.StaffPermissions.Count());

        await service.UpdateAssignmentAsync(new UpdatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeX },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionsToAdd = new List<PermissionOverrideModel> { DenyAction(resourceId, "View") }
        });

        Assert.Empty(db.StaffPermissions);
    }

    [Fact]
    public async Task CreateAssignment_WithSemesterScope_StampsExpiresAtToSemesterEnd()
    {
        // The end of the temporal scope (Semester.EndDate) is stamped onto ExpiresAt
        // at write time, so the manual sweep can later prune it.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var semId = Guid.NewGuid();
        var semEnd = DateTime.UtcNow.AddDays(30);

        db.AcademicYears.Add(new AcademicYear { Id = yearId, Name = "Y", StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(60) });
        db.Semesters.Add(new Semester { Id = semId, AcademicYearId = yearId, Name = "S", StartDate = DateTime.UtcNow.AddDays(-1), EndDate = semEnd });
        await db.SaveChangesAsync();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AcademicYearId = yearId, SemesterId = semId, AlwaysActive = false },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        var row = db.StaffPermissions.Single();
        Assert.NotNull(row.ExpiresAt);
        Assert.Equal(semEnd, row.ExpiresAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAssignment_AlwaysActive_LeavesExpiresAtNull()
    {
        // A Global / AlwaysActive temporal scope has no end, so the override never expires.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        Assert.All(db.StaffPermissions, sp => Assert.Null(sp.ExpiresAt));
    }

    [Fact]
    public async Task ExpireOverridesAsync_HardDeletesPastDue_KeepsFutureAndGlobal()
    {
        // The manual trigger hard-deletes rows whose end-of-temporal-scope is now-or-past,
        // and returns the count removed; future and never-expiring rows are untouched.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();

        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "View", OverrideType.Allow, "Global", "Global") { ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "Insert", OverrideType.Allow, "Global", "Global") { ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "EditClose", OverrideType.Allow, "Global", "Global") { ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "Open", OverrideType.Allow, "Global", "Global") { ExpiresAt = null });
        await db.SaveChangesAsync();

        var deleted = await service.ExpireOverridesAsync();

        Assert.Equal(2, deleted);
        var remaining = db.StaffPermissions.Select(sp => sp.Action).OrderBy(a => a).ToList();
        Assert.Equal(new[] { "EditClose", "Open" }, remaining);
    }

    [Fact]
    public async Task ExpireOverrides_EndToEnd_SemesterAlreadyEnded_IsPrunedByManualTrigger()
    {
        // Headline scenario: an override scoped to a semester that already ended gets a
        // past ExpiresAt at write time, and the manual trigger prunes it so it no longer
        // appears in the effective set.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var semId = Guid.NewGuid();

        db.AcademicYears.Add(new AcademicYear { Id = yearId, Name = "Y", StartDate = DateTime.UtcNow.AddDays(-120), EndDate = DateTime.UtcNow.AddDays(-1) });
        db.Semesters.Add(new Semester { Id = semId, AcademicYearId = yearId, Name = "S", StartDate = DateTime.UtcNow.AddDays(-60), EndDate = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AcademicYearId = yearId, SemesterId = semId },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });
        Assert.NotEmpty(db.StaffPermissions);

        var deleted = await service.ExpireOverridesAsync();

        Assert.Equal(1, deleted);
        Assert.Empty(db.StaffPermissions);
        Assert.Empty(await service.GetEffectivePermissionsAsync(userId));
    }

    [Fact]
    public async Task GetEffectivePermissions_ExcludesExpiredOverride_BeforeSweep()
    {
        // Read-time filtering: an override past its ExpiresAt stops contributing to
        // the effective set immediately, without waiting for ExpireOverridesAsync.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();

        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "View", OverrideType.Allow, "Global", "Global")
        {
            StructureNodeId = nodeX,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // expired yesterday
        });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "Insert", OverrideType.Allow, "Global", "Global")
        {
            StructureNodeId = nodeX,
            ExpiresAt = DateTime.UtcNow.AddDays(1) // still live
        });
        await db.SaveChangesAsync();

        var eff = await service.GetEffectivePermissionsAsync(userId);

        Assert.DoesNotContain(eff, p => p.Action == "View");
        Assert.Contains(eff, p => p.Action == "Insert");
        // Filtering is read-time, not deletion — both rows are still physically present.
        Assert.Equal(2, db.StaffPermissions.Count());
    }

    [Fact]
    public async Task ExpiredDenyOverride_NoLongerSuppressesRoleGrant()
    {
        // Security-relevant direction: an expired DENY must stop carving out the
        // role grant, so the underlying permission comes back once the deny lapses.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var nodeX = Guid.NewGuid();

        db.RolePermissions.Add(new RolePermission(roleId, resourceId, "Delete"));
        db.StaffRoles.Add(new StaffRoleAssignment(userId, roleId, "Global", "Global") { StructureNodeId = nodeX });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "Delete", OverrideType.Deny, "Global", "Global")
        {
            StructureNodeId = nodeX,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // deny has lapsed
        });
        await db.SaveChangesAsync();

        var eff = await service.GetEffectivePermissionsAsync(userId);

        // Deny is expired → excluded → role's Delete grant is effective again.
        Assert.Contains(eff, p => p.Action == "Delete");
    }

    [Fact]
    public async Task BackfillOverrideExpiryAsync_FillsLegacyScopedRows_LeavesGlobalNull()
    {
        // Legacy rows written before ExpiresAt was stamped: a bounded-semester row
        // gets its window end filled in; a Global row stays null (never expires).
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var semId = Guid.NewGuid();
        var semEnd = DateTime.UtcNow.AddDays(10);

        db.AcademicYears.Add(new AcademicYear { Id = yearId, Name = "Y", StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(40) });
        db.Semesters.Add(new Semester { Id = semId, AcademicYearId = yearId, Name = "S", StartDate = DateTime.UtcNow.AddDays(-5), EndDate = semEnd });

        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "View", OverrideType.Allow, yearId.ToString(), semId.ToString()) { ExpiresAt = null });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "Insert", OverrideType.Allow, "Global", "Global") { ExpiresAt = null });
        await db.SaveChangesAsync();

        var updated = await service.BackfillOverrideExpiryAsync();

        Assert.Equal(1, updated);
        var scoped = db.StaffPermissions.Single(sp => sp.Action == "View");
        Assert.NotNull(scoped.ExpiresAt);
        Assert.Equal(semEnd, scoped.ExpiresAt!.Value, TimeSpan.FromSeconds(1));
        var global = db.StaffPermissions.Single(sp => sp.Action == "Insert");
        Assert.Null(global.ExpiresAt);
    }

    [Fact]
    public async Task BackfillOverrideExpiryAsync_ThenExpire_PrunesLapsedLegacyRow()
    {
        // End-to-end: a legacy null-expiry row scoped to an already-ended semester is
        // backfilled to a past ExpiresAt, then removed by the manual sweep.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var semId = Guid.NewGuid();

        db.AcademicYears.Add(new AcademicYear { Id = yearId, Name = "Y", StartDate = DateTime.UtcNow.AddDays(-120), EndDate = DateTime.UtcNow.AddDays(-1) });
        db.Semesters.Add(new Semester { Id = semId, AcademicYearId = yearId, Name = "S", StartDate = DateTime.UtcNow.AddDays(-60), EndDate = DateTime.UtcNow.AddDays(-1) });
        db.StaffPermissions.Add(new StaffPermissionOverride(userId, resourceId, "View", OverrideType.Allow, yearId.ToString(), semId.ToString()) { ExpiresAt = null });
        await db.SaveChangesAsync();

        var backfilled = await service.BackfillOverrideExpiryAsync();
        Assert.Equal(1, backfilled);

        var deleted = await service.ExpireOverridesAsync();
        Assert.Equal(1, deleted);
        Assert.Empty(db.StaffPermissions);
    }

    // ---------------------------------------------------------------------
    // Per-override scope in a SINGLE assignment, write-time action expansion,
    // and the role-permission management contract.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateAssignment_OverrideWithOwnScope_PersistsAtThatScope_NotTheRoleScope()
    {
        // One call: role at the request scope, but the override carries its OWN scope.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var nodeRole = Guid.NewGuid();
        var nodeOverride = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            RoleIds = new List<Guid> { roleId },
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeRole },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel>
            {
                new PermissionOverrideModel
                {
                    ResourceId = resourceId,
                    Type = OverrideType.Allow,
                    Actions = new List<string> { "View" },
                    StructuralScope = new StructuralScopeModel { StructureNodeId = nodeOverride },
                }
            }
        });

        var role = db.StaffRoles.Single();
        Assert.Equal(nodeRole, role.StructureNodeId);

        var ov = db.StaffPermissions.Single();
        Assert.Equal(nodeOverride, ov.StructureNodeId); // its OWN scope, not nodeRole
        Assert.Equal("View", ov.Action);
    }

    [Fact]
    public async Task CreateAssignment_OverrideWithoutScope_InheritsRequestScope()
    {
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var nodeRole = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeRole },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "View") }
        });

        var ov = db.StaffPermissions.Single();
        Assert.Equal(nodeRole, ov.StructureNodeId); // inherited from the request scope
    }

    [Fact]
    public async Task CreateAssignment_AllowActions_ExpandForwardClosureAtWrite()
    {
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { AllowAction(resourceId, "EditClose") }
        });

        // Allow EditClose -> forward closure {View, Insert, EditClose}.
        var actions = db.StaffPermissions.Where(sp => sp.Type == OverrideType.Allow).Select(sp => sp.Action).OrderBy(a => a).ToList();
        Assert.Equal(new[] { "EditClose", "Insert", "View" }, actions);
    }

    [Fact]
    public async Task CreateAssignment_DenyActions_ExpandReverseClosureAtWrite()
    {
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            StructuralScope = new StructuralScopeModel(),
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel> { DenyAction(resourceId, "EditClose") }
        });

        // Deny EditClose -> reverse closure {EditClose, Open, Delete} (verbs that grant it).
        var actions = db.StaffPermissions.Where(sp => sp.Type == OverrideType.Deny).Select(sp => sp.Action).OrderBy(a => a).ToList();
        Assert.Equal(new[] { "Delete", "EditClose", "Open" }, actions);
    }

    [Fact]
    public async Task SetRolePermissions_FullReplace_ExpandsForwardAndReconciles()
    {
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var roleId = Guid.NewGuid();
        db.Roles.Add(new Role { Id = roleId, Name = "R" });
        // Pre-existing grant that full-replace should drop.
        db.RolePermissions.Add(new RolePermission(roleId, resourceId, "Delete"));
        await db.SaveChangesAsync();

        var registry = new PermissionManifestRegistry(new[] { (IPermissionManifest)new TestPermissionsManifest() });
        var expander = new ManifestActionExpander(registry);
        var currentUser = new Mock<ICurrentUser>(); // Id defaults to Guid.Empty -> permission check skipped
        var permissions = new Mock<IPermissionManagementService>();
        var handler = new SetRolePermissionsCommandHandler(db, permissions.Object, currentUser.Object, expander);

        var resp = await handler.Handle(new SetRolePermissionsRequest
        {
            RoleId = roleId,
            Resources = new List<RoleResourcePermissionsModel>
            {
                new RoleResourcePermissionsModel { ResourceId = resourceId, Actions = new List<string> { "EditClose" } }
            }
        }, default);

        Assert.NotNull(resp);
        // EditClose forward closure {View, Insert, EditClose}; the old Delete grant is gone.
        var actions = db.RolePermissions.Where(rp => rp.RoleId == roleId).Select(rp => rp.Action).OrderBy(a => a).ToList();
        Assert.Equal(new[] { "EditClose", "Insert", "View" }, actions);
    }

    [Fact]
    public async Task SetRolePermissions_UnknownRole_ReturnsNull()
    {
        var db = GetDbContext();
        var registry = new PermissionManifestRegistry(new[] { (IPermissionManifest)new TestPermissionsManifest() });
        var expander = new ManifestActionExpander(registry);
        var handler = new SetRolePermissionsCommandHandler(
            db, new Mock<IPermissionManagementService>().Object, new Mock<ICurrentUser>().Object, expander);

        var resp = await handler.Handle(new SetRolePermissionsRequest { RoleId = Guid.NewGuid() }, default);

        Assert.Null(resp);
    }

    [Fact]
    public async Task GetAssignment_SurfacesOverrideFromAnotherScope_WithItsOwnScope()
    {
        // Reshape: querying the ROLE scope still surfaces an override that was re-scoped
        // to a DIFFERENT scope, tagged with its own scope.
        var db = GetDbContext();
        var resourceId = SeedPermissionsResource(db);
        var service = CreateServiceWithCache(db, new PassThroughCache());

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var nodeRole = Guid.NewGuid();
        var nodeOverride = Guid.NewGuid();

        await service.CreateAssignmentAsync(new CreatePermissionAssignmentRequest
        {
            UserId = userId,
            RoleIds = new List<Guid> { roleId },
            StructuralScope = new StructuralScopeModel { StructureNodeId = nodeRole },
            TemporalScope = new TemporalScopeModel { AlwaysActive = true },
            PermissionOverrides = new List<PermissionOverrideModel>
            {
                new PermissionOverrideModel
                {
                    ResourceId = resourceId,
                    Type = OverrideType.Allow,
                    Actions = new List<string> { "View" },
                    StructuralScope = new StructuralScopeModel { StructureNodeId = nodeOverride },
                }
            }
        });

        var assignment = await service.GetAssignmentAsync(new GetPermissionAssignmentQueryDto
        {
            UserId = userId,
            StructureNodeId = nodeRole, // query the ROLE scope, not the override's scope
            AlwaysActive = true,
        });

        Assert.NotNull(assignment);
        Assert.Contains(roleId, assignment!.RoleIds);
        var ov = Assert.Single(assignment.PermissionOverrides);
        Assert.Equal(resourceId, ov.ResourceId);
        Assert.Equal(nodeOverride, ov.StructuralScope?.StructureNodeId); // surfaced with its OWN scope
        Assert.Contains("View", ov.Actions);
    }

    private sealed class TestPermissionsManifest : IPermissionManifest
    {
        public string Module => "permissions";
        public string DisplayName => "Permissions";
        public string? Icon => null;
        public int? OrderNumber => 0;
        public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
        {
            ResourceDefinition.WithCrudActions("permissions", "Permissions", 0),
        };
    }
}