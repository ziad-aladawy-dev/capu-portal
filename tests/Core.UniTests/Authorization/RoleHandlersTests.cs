using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;
using CapitalUniversity.Core.UniTests._Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Handlers tests for Role commands and queries.
/// </summary>
public class RoleHandlersTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateRole_PersistsCustomRoleAndReturnsResponse()
    {
        using var db = NewDb();
        var handler = new CreateRoleCommandHandler(db, new TestLocalizationService());

        var response = await handler.Handle(new CreateRoleRequest { Name = "Auditor" }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Auditor", response.Name);

        var reloaded = await db.Roles.FindAsync(response.Id);
        Assert.Equal("{\"ar\":\"Auditor\",\"en\":\"Auditor\"}", reloaded!.Name);
    }

    [Fact]
    public async Task UpdateRole_ExistingRole_RenamesAndPersists()
    {
        using var db = NewDb();
        var role = new Role { Name = "Old", IsSystemRole = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(db, new TestLocalizationService());
        var response = await handler.Handle(
            new UpdateRoleRequest { Id = role.Id, Name = "New" },
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(role.Id, response!.Id);
        Assert.Equal("New", response.Name);

        var reloaded = await db.Roles.FindAsync(role.Id);
        Assert.Equal("{\"ar\":\"New\",\"en\":\"New\"}", reloaded!.Name);
    }

    [Fact]
    public async Task UpdateRole_MissingRole_ReturnsNull()
    {
        using var db = NewDb();
        var handler = new UpdateRoleCommandHandler(db, new TestLocalizationService());

        var response = await handler.Handle(
            new UpdateRoleRequest { Id = Guid.NewGuid(), Name = "ghost" },
            CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task DeleteRole_ExistingRole_RemovesAndInvokesInvalidator()
    {
        using var db = NewDb();
        var role = new Role { Name = "ToDelete", IsSystemRole = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var invalidator = new Mock<IPermissionCacheInvalidator>(MockBehavior.Strict);
        invalidator
            .Setup(i => i.InvalidateRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var handler = new DeleteRoleCommandHandler(db, invalidator.Object);
        var deleted = await handler.Handle(new DeleteRoleRequest { Id = role.Id }, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await db.Roles.FindAsync(role.Id));
        invalidator.Verify();
    }

    [Fact]
    public async Task GetRoleById_Existing_ReturnsResponse()
    {
        using var db = NewDb();
        var role = new Role { Name = "Auditor", IsSystemRole = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var handler = new GetRoleByIdQueryHandler(db, new TestLocalizationService());
        var response = await handler.Handle(new GetRoleByIdRequest { Id = role.Id }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(role.Id, response!.Id);
        Assert.Equal("Auditor", response.Name);
    }

    [Fact]
    public async Task GetRoles_ReturnsAll()
    {
        using var db = NewDb();
        db.Roles.Add(new Role { Name = "R1", IsSystemRole = false });
        db.Roles.Add(new Role { Name = "R2", IsSystemRole = false });
        await db.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(db, new TestLocalizationService());
        var response = await handler.Handle(new GetRolesRequest(), CancellationToken.None);

        Assert.Equal(2, response.TotalCount);
        Assert.Contains(response.Items, r => r.Name == "R1");
        Assert.Contains(response.Items, r => r.Name == "R2");
    }

    [Fact]
    public async Task GetRoles_Empty_ReturnsEmptyResponse()
    {
        using var db = NewDb();
        var handler = new GetRolesQueryHandler(db, new TestLocalizationService());

        var response = await handler.Handle(new GetRolesRequest(), CancellationToken.None);

        Assert.Equal(0, response.TotalCount);
        Assert.Empty(response.Items);
    }
}
