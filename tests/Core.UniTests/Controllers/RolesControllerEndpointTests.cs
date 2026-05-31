using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;
using CapitalUniversity.Core.UniTests._Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class RolesControllerEndpointTests
{
    private static (RolesController ctrl, CoreDbContext db) BuildController(
        IPermissionCacheInvalidator? invalidator = null)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CoreDbContext(options);
        db.Database.EnsureCreated();

        var localization = new TestLocalizationService();
        var permissions = new Mock<IPermissionManagementService>().Object;
        var currentUser = new Mock<ICurrentUser>().Object;

        var ctrl = new RolesController(
            new CreateRoleCommandHandler(db, localization, permissions, currentUser),
            new UpdateRoleCommandHandler(db, localization, permissions, currentUser),
            new DeleteRoleCommandHandler(db, permissions, currentUser, invalidator),
            new GetRoleByIdQueryHandler(db, localization),
            new GetRolesQueryHandler(db, localization));
        return (ctrl, db);
    }

    [Fact]
    public async Task GetRoles_ReturnsOkWithPagedResponse()
    {
        var (ctrl, db) = BuildController();
        db.Roles.Add(new Role { Name = "Auditor" });
        await db.SaveChangesAsync();

        var result = await ctrl.GetRoles(new GetRolesRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedRoleResponse>(ok.Value);
        Assert.Equal(1, paged.TotalCount);
    }

    [Fact]
    public async Task GetRole_Existing_ReturnsOk()
    {
        var (ctrl, db) = BuildController();
        var role = new Role { Name = "Reader" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var result = await ctrl.GetRole(role.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<RoleResponse>(ok.Value);
        Assert.Equal(role.Id, body.Id);
    }

    [Fact]
    public async Task GetRole_Missing_ReturnsNotFound()
    {
        var (ctrl, _) = BuildController();
        var result = await ctrl.GetRole(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateRole_ReturnsCreatedAtAction()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.CreateRole(new CreateRoleRequest { Name = "Writer" }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ctrl.GetRole), created.ActionName);
        Assert.NotNull(created.Value);
    }

    [Fact]
    public async Task UpdateRole_MismatchedIds_ReturnsBadRequest()
    {
        var (ctrl, _) = BuildController();
        var routeId = Guid.NewGuid();
        var bodyId  = Guid.NewGuid();

        var result = await ctrl.UpdateRole(routeId, new UpdateRoleRequest { Id = bodyId, Name = "x" }, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result.Result);
    }

    [Fact]
    public async Task UpdateRole_Existing_ReturnsOk()
    {
        var (ctrl, db) = BuildController();
        var role = new Role { Name = "Old" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var result = await ctrl.UpdateRole(role.Id, new UpdateRoleRequest { Id = role.Id, Name = "New" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<UpdateRoleResponse>(ok.Value);
        Assert.Equal("New", body.Name);
    }

    [Fact]
    public async Task UpdateRole_Missing_ReturnsNotFound()
    {
        var (ctrl, _) = BuildController();
        var id = Guid.NewGuid();

        var result = await ctrl.UpdateRole(id, new UpdateRoleRequest { Id = id, Name = "x" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteRole_Existing_ReturnsNoContent()
    {
        var invalidator = new Mock<IPermissionCacheInvalidator>();
        invalidator.Setup(i => i.InvalidateRoleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var (ctrl, db) = BuildController(invalidator.Object);
        var role = new Role { Name = "Tmp" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var result = await ctrl.DeleteRole(role.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteRole_Missing_ReturnsNotFound()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.DeleteRole(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}