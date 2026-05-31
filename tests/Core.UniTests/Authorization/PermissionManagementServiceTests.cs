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
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
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
                    Level = ActionLevel.EditClose,
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