using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class AuthorizationControllerTests
{
    private static AuthorizationController NewController(out Mock<IPermissionTreeQueryHandler> handler)
    {
        handler = new Mock<IPermissionTreeQueryHandler>(MockBehavior.Strict);
        return new AuthorizationController(handler.Object, NewSetRoleHandler());
    }

    // The GET endpoints under test don't touch the role-permission writer; build a
    // minimal real instance just to satisfy the controller constructor.
    private static SetRolePermissionsCommandHandler NewSetRoleHandler()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CoreDbContext(options);
        var expander = new ManifestActionExpander(new PermissionManifestRegistry(Array.Empty<IPermissionManifest>()));
        return new SetRolePermissionsCommandHandler(
            db, new Mock<IPermissionManagementService>().Object, new Mock<ICurrentUser>().Object, expander);
    }

    [Fact]
    public async Task GetPermissionTree_ReturnsOk()
    {
        var ctrl = NewController(out var handler);
        var tree = new List<ModulePermissionTreeDto> { new() { ModuleName = "M" } };
        handler.Setup(h => h.Handle(It.IsAny<GetPermissionTreeRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(tree);

        var result = await ctrl.GetPermissionTree(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(tree, ok.Value);
    }

    [Fact]
    public async Task GetRolePermissions_KnownRole_ReturnsOk()
    {
        var ctrl = NewController(out var handler);
        var roleId = Guid.NewGuid();
        var tree = new List<ModulePermissionTreeDto> { new() };
        handler.Setup(h => h.Handle(It.Is<GetRolePermissionsRequest>(r => r.RoleId == roleId), It.IsAny<CancellationToken>()))
               .ReturnsAsync(tree);

        var result = await ctrl.GetRolePermissions(roleId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(tree, ok.Value);
    }
}