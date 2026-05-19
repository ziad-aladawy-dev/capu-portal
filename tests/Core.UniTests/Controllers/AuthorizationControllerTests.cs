using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class AuthorizationControllerTests
{
    private static AuthorizationController NewController(out Mock<IPermissionTreeQueryHandler> handler)
    {
        handler = new Mock<IPermissionTreeQueryHandler>(MockBehavior.Strict);
        return new AuthorizationController(handler.Object);
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

    [Fact]
    public async Task GetRolePermissions_UnknownRole_ReturnsNotFound()
    {
        var ctrl = NewController(out var handler);
        var roleId = Guid.NewGuid();
        handler.Setup(h => h.Handle(It.IsAny<GetRolePermissionsRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((List<ModulePermissionTreeDto>?)null);

        var result = await ctrl.GetRolePermissions(roleId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
