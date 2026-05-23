using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class PermissionsControllerTests
{
    [Fact]
    public async Task GetEffectivePermissions_ReturnsOkWithPermissions()
    {
        var mockService = new Mock<IPermissionManagementService>();
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.Id).Returns(Guid.NewGuid());

        var permissions = new List<PermissionDto>
        {
            new PermissionDto { Module = "Student", Resource = "Profile", Action = "View" }
        };

        mockService.Setup(s => s.GetEffectivePermissionsAsync(mockUser.Object.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var controller = new PermissionsController(mockService.Object, mockUser.Object);

        var result = await controller.GetEffectivePermissions(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedPermissions = Assert.IsType<List<PermissionDto>>(okResult.Value);
        Assert.Single(returnedPermissions);
    }

    [Fact]
    public async Task GetAssignment_NotFound_ReturnsOkWithNull()
    {
        var mockService = new Mock<IPermissionManagementService>();
        var mockUser = new Mock<ICurrentUser>();

        mockService.Setup(s => s.GetAssignmentAsync(It.IsAny<GetPermissionAssignmentQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PermissionAssignmentResponse?)null);

        var controller = new PermissionsController(mockService.Object, mockUser.Object);

        var result = await controller.GetAssignment(new GetPermissionAssignmentQueryDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(okResult.Value);
    }

    [Fact]
    public async Task GetAssignment_Found_ReturnsOkResult()
    {
        var mockService = new Mock<IPermissionManagementService>();
        var mockUser = new Mock<ICurrentUser>();

        var response = new PermissionAssignmentResponse();

        mockService.Setup(s => s.GetAssignmentAsync(It.IsAny<GetPermissionAssignmentQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new PermissionsController(mockService.Object, mockUser.Object);

        var result = await controller.GetAssignment(new GetPermissionAssignmentQueryDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, okResult.Value);
    }
}
