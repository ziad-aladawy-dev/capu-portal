using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class PermissionTreeQueryHandlerTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("PermissionTree_" + Guid.NewGuid())
            .Options);

    private static PermissionTreeQueryHandler NewHandler(CoreDbContext db, IPermissionManifestRegistry? registry = null)
    {
        var mockPermSvc = new Mock<IPermissionManagementService>();
        return new(db, registry ?? BuildRegistry(), new PassthroughLocalization(), mockPermSvc.Object);
    }

    /// <summary>
    /// Minimal stand-in for <see cref="ILocalizationService"/>. The handler
    /// only uses two methods — <c>Get&lt;string&gt;(json)</c> to decode the
    /// DisplayName JSON (or pass through legacy literals) and
    /// <c>GetString(key)</c> to render action descriptions — so the fake
    /// echoes the input. Tests in this file don't assert on description
    /// content; using a real LocalizationService would just add wiring.
    /// </summary>
    private sealed class PassthroughLocalization : ILocalizationService
    {
        public T Get<T>(string json) =>
            json is T direct ? direct : default!;
        public string Get(Enum value) => value.ToString();
        public string GetString(string key) => key;
        public bool ContainsKey(string? key) => false;
    }

    private static IPermissionManifestRegistry BuildRegistry(string moduleKey = "academics", string resourceKey = "academic-years")
    {
        return new PermissionManifestRegistry(new[]
        {
            (IPermissionManifest)new TestCrudManifest(moduleKey, resourceKey, "Academic Timeline"),
        });
    }

    private static async Task<(Module module, Resource resource)> SeedOneModuleWithResourceAsync(
        CoreDbContext db, string moduleKey = "academics", string resourceKey = "academic-years", string resourceDisplay = "Academic Timeline")
    {
        var module = new Module { ModuleKey = moduleKey, DisplayName = "Academics" };
        var resource = new Resource { Module = module, ModuleId = module.Id, Key = resourceKey, DisplayName = resourceDisplay };
        db.Modules.Add(module);
        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return (module, resource);
    }

    [Fact]
    public async Task GetPermissionTree_ReturnsModulesWithResourcesAndUnassignedActions()
    {
        using var db = NewDb();
        var (module, resource) = await SeedOneModuleWithResourceAsync(db);

        var sut = NewHandler(db);
        var tree = await sut.Handle(new GetPermissionTreeRequest(), CancellationToken.None);

        var moduleDto = Assert.Single(tree);
        Assert.Equal(module.Id, moduleDto.ModuleId);
        var resourceDto = Assert.Single(moduleDto.Resources);
        Assert.Equal(resource.Id, resourceDto.ResourceId);
        Assert.Equal(5, resourceDto.Permissions.Count);
        Assert.All(resourceDto.Permissions, p => Assert.Null(p.IsAssigned));
        Assert.Contains(resourceDto.Permissions, p => p.Action == "View");
        Assert.Contains(resourceDto.Permissions, p => p.Action == "Delete");
    }

    [Fact]
    public async Task GetRolePermissions_UnknownRole_ThrowsNotFoundException()
    {
        using var db = NewDb();
        var sut = NewHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Handle(new GetRolePermissionsRequest { RoleId = Guid.NewGuid() }, CancellationToken.None));
    }

    [Fact]
    public async Task GetRolePermissions_KnownRole_FlagsAssignedActionsOnly()
    {
        using var db = NewDb();
        var (_, resource) = await SeedOneModuleWithResourceAsync(db);
        var role = new Role { Name = "TestRole" };
        db.Roles.Add(role);
        // Per-action storage: explicit View+Insert+EditClose rows.
        db.AddCrudGrant(role.Id, resource.Id, ActionLevel.EditClose);
        await db.SaveChangesAsync();

        var sut = NewHandler(db);
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
    public async Task GetRolePermissions_UnionsAcrossMultipleRows()
    {
        using var db = NewDb();
        var (_, resource) = await SeedOneModuleWithResourceAsync(db);
        var role = new Role { Name = "DupRole" };
        db.Roles.Add(role);
        // Per-action storage with multiple rows — the handler should union actions.
        db.RolePermissions.AddRange(
            new RolePermission(role.Id, resource.Id, "View"),
            new RolePermission(role.Id, resource.Id, "Delete"));
        await db.SaveChangesAsync();

        var sut = NewHandler(db);
        var tree = await sut.Handle(new GetRolePermissionsRequest { RoleId = role.Id }, CancellationToken.None);

        var resourceDto = Assert.Single(Assert.Single(tree!).Resources);
        Assert.True(resourceDto.Permissions.Single(p => p.Action == "View").IsAssigned);
        Assert.False(resourceDto.Permissions.Single(p => p.Action == "Insert").IsAssigned);
        Assert.True(resourceDto.Permissions.Single(p => p.Action == "Delete").IsAssigned);
    }

    [Fact]
    public async Task GetPermissionTree_ModuleWithNoResources_ReturnsEmptyResourceList()
    {
        using var db = NewDb();
        db.Modules.Add(new Module { ModuleKey = "lonely", DisplayName = "Lonely" });
        await db.SaveChangesAsync();

        var sut = NewHandler(db);
        var tree = await sut.Handle(new GetPermissionTreeRequest(), CancellationToken.None);

        var moduleDto = Assert.Single(tree);
        Assert.Empty(moduleDto.Resources);
    }

    private sealed class TestCrudManifest : IPermissionManifest
    {
        private readonly string _module;
        private readonly string _resource;
        private readonly string _display;
        public TestCrudManifest(string module, string resource, string display)
        {
            _module = module;
            _resource = resource;
            _display = display;
            Resources = new[] { ResourceDefinition.WithCrudActions(resource, display, 0) };
        }
        public string Module => _module;
        public string DisplayName => _module;
        public string? Icon => null;
        public int? OrderNumber => 0;
        public IReadOnlyCollection<ResourceDefinition> Resources { get; }
    }
}
