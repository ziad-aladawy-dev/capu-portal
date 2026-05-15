using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class PermissionManagementServiceTests
{
    private CoreDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var ctx = new CoreDbContext(options);
        // Ensure creation logic doesn't trigger relational extensions that fail with InMemory provider
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private PermissionManagementService CreateService(
        CoreDbContext dbContext,
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

        return new PermissionManagementService(
            mockPermissionService.Object,
            mockRequestContext.Object,
            mockScopeResolver.Object,
            dbContext,
            mockCache.Object,
            mockCurrentUser.Object);
    }

    [Fact]
    public async Task CreateAssignmentAsync_ValidRequest_CreatesRolesAndOverrides()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = CreateService(dbContext, out _, out _, out _, out _, out _);

        var request = new CreatePermissionAssignmentRequest
        {
            UserId = Guid.NewGuid(),
            RoleIds = new List<Guid> { Guid.NewGuid() },
            PermissionOverrides = new List<PermissionOverrideModel>
            {
                new PermissionOverrideModel
                {
                    ServiceId = Guid.NewGuid(),
                    Resource = "Profile",
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
        Assert.Single(dbContext.StaffPermissions);

        var savedRole = dbContext.StaffRoles.First();
        Assert.Equal(request.UserId, savedRole.StaffId);
        Assert.Equal(request.StructuralScope.StructureNodeId.ToString(), savedRole.StructureNodeId.ToString());
        Assert.Equal("Global", savedRole.Year);
        Assert.Equal("Global", savedRole.Semester);

        var savedOverride = dbContext.StaffPermissions.First();
        Assert.Equal("Profile", savedOverride.Resource);
        Assert.Equal(ActionLevel.EditClose, savedOverride.Level);
        Assert.Equal(request.StructuralScope.StructureNodeId.ToString(), savedOverride.StructureNodeId.ToString());
    }

    [Fact]
    public async Task GetAssignmentAsync_WhenExists_ReturnsAssignment()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = CreateService(dbContext, out _, out _, out _, out _, out _);

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
        var service = CreateService(dbContext, out _, out _, out _, out _, out _);

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
}
