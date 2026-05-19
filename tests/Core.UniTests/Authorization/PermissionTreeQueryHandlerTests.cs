using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class PermissionTreeQueryHandlerTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("PermissionTree_" + Guid.NewGuid())
            .Options);

    private static async Task<(Module module, Service service)> SeedOneModuleWithServiceAsync(
        CoreDbContext db, string moduleKey = "academics", string serviceName = "Manage Academic Years")
    {
        var module = new Module { ModuleKey = moduleKey, DisplayName = "Academics" };
        var service = new Service { Module = module, ModuleId = module.Id, DisplayName = serviceName };
        db.Modules.Add(module);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return (module, service);
    }

    [Fact]
    public async Task GetPermissionTree_ReturnsModulesWithResourcesAndUnassignedActions()
    {
        using var db = NewDb();
        var (module, service) = await SeedOneModuleWithServiceAsync(db);

        var sut = new PermissionTreeQueryHandler(db);
        var tree = await sut.Handle(new GetPermissionTreeRequest(), CancellationToken.None);

        var moduleDto = Assert.Single(tree);
        Assert.Equal(module.Id, moduleDto.ModuleId);
        var resourceDto = Assert.Single(moduleDto.Resources);
        Assert.Equal(service.Id, resourceDto.ResourceId);
        Assert.Equal(5, resourceDto.Permissions.Count);
        Assert.All(resourceDto.Permissions, p => Assert.Null(p.IsAssigned));
        Assert.Contains(resourceDto.Permissions, p => p.Action == "View");
        Assert.Contains(resourceDto.Permissions, p => p.Action == "Delete");
    }

    [Fact]
    public async Task GetRolePermissions_UnknownRole_ReturnsNull()
    {
        using var db = NewDb();
        var sut = new PermissionTreeQueryHandler(db);

        var result = await sut.Handle(new GetRolePermissionsRequest { RoleId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRolePermissions_KnownRole_FlagsAssignedAtOrBelowGrantedLevel()
    {
        using var db = NewDb();
        var (module, service) = await SeedOneModuleWithServiceAsync(db);
        var role = new Role { Name = "TestRole" };
        db.Roles.Add(role);
        // Grant the role EditClose on this service → View+Insert+EditClose flagged true; Open+Delete false.
        db.RolePermissions.Add(new RolePermission(role.Id, service.Id, "academic-years", ActionLevel.EditClose));
        await db.SaveChangesAsync();

        var sut = new PermissionTreeQueryHandler(db);
        var tree = await sut.Handle(new GetRolePermissionsRequest { RoleId = role.Id }, CancellationToken.None);

        Assert.NotNull(tree);
        var resourceDto = Assert.Single(Assert.Single(tree!).Resources);

        Assert.True(resourceDto.Permissions.Single(p => p.Action == "View").IsAssigned);
        Assert.True(resourceDto.Permissions.Single(p => p.Action == "Insert").IsAssigned);
        Assert.True(resourceDto.Permissions.Single(p => p.Action == "EditClose").IsAssigned);
        Assert.False(resourceDto.Permissions.Single(p => p.Action == "Open").IsAssigned);
        Assert.False(resourceDto.Permissions.Single(p => p.Action == "Delete").IsAssigned);
    }

    [Fact]
    public async Task GetRolePermissions_TakesMaxLevelWhenDuplicateRowsExist()
    {
        using var db = NewDb();
        var (_, service) = await SeedOneModuleWithServiceAsync(db);
        var role = new Role { Name = "DupRole" };
        db.Roles.Add(role);
        db.RolePermissions.AddRange(
            new RolePermission(role.Id, service.Id, "academic-years", ActionLevel.View),
            new RolePermission(role.Id, service.Id, "academic-years", ActionLevel.Delete));
        await db.SaveChangesAsync();

        var sut = new PermissionTreeQueryHandler(db);
        var tree = await sut.Handle(new GetRolePermissionsRequest { RoleId = role.Id }, CancellationToken.None);

        var resourceDto = Assert.Single(Assert.Single(tree!).Resources);
        // Max(View, Delete) = Delete → all actions assigned.
        Assert.All(resourceDto.Permissions, p => Assert.True(p.IsAssigned));
    }

    [Fact]
    public async Task GetPermissionTree_ModuleWithNoServices_ReturnsEmptyResourceList()
    {
        using var db = NewDb();
        db.Modules.Add(new Module { ModuleKey = "lonely", DisplayName = "Lonely" });
        await db.SaveChangesAsync();

        var sut = new PermissionTreeQueryHandler(db);
        var tree = await sut.Handle(new GetPermissionTreeRequest(), CancellationToken.None);

        var moduleDto = Assert.Single(tree);
        Assert.Empty(moduleDto.Resources);
    }
}
