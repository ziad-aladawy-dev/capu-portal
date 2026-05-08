using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Auth.Authorization.DTOs;
using CapitalUniversity.Core.CrossCutting.Security;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

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

    [Fact]
    public async Task CreateAssignmentAsync_ValidRequest_CreatesRolesAndOverrides()
    {
        // Arrange
        var dbContext = GetDbContext();
        var mockPermissionService = new Mock<IPermissionService>();
        var mockRequestContext = new Mock<IRequestContext>();
        var mockScopeResolver = new Mock<IScopeResolver>();

        var service = new PermissionManagementService(mockPermissionService.Object, mockRequestContext.Object, mockScopeResolver.Object, dbContext);

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
            StructuralScope = new StructuralScopeModel { FacultyId = Guid.NewGuid(), AllFaculties = false },
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
        Assert.Equal(request.StructuralScope.FacultyId.ToString(), savedRole.Domain);
        Assert.Equal("Global", savedRole.Year);
        Assert.Equal("Global", savedRole.Semester);

        var savedOverride = dbContext.StaffPermissions.First();
        Assert.Equal("Profile", savedOverride.Resource);
        Assert.Equal(ActionLevel.EditClose, savedOverride.Level);
        Assert.Equal(request.StructuralScope.FacultyId.ToString(), savedOverride.Domain);
    }

    [Fact]
    public async Task CreateAssignmentAsync_InvalidWildcardCombination_ThrowsArgumentException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var mockPermissionService = new Mock<IPermissionService>();
        var mockRequestContext = new Mock<IRequestContext>();
        var mockScopeResolver = new Mock<IScopeResolver>();

        var service = new PermissionManagementService(mockPermissionService.Object, mockRequestContext.Object, mockScopeResolver.Object, dbContext);

        var request = new CreatePermissionAssignmentRequest
        {
            UserId = Guid.NewGuid(),
            StructuralScope = new StructuralScopeModel { FacultyId = Guid.NewGuid(), AllFaculties = true } // Invalid combination
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAssignmentAsync(request));
    }
}
