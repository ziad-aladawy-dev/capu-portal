using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class RoleHandlersTests
{
    private static readonly string[] ExpectedFirstPageNames = { "Alpha", "Bravo" };

    private static CoreDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var ctx = new CoreDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task CreateRole_PersistsCustomRoleAndReturnsResponse()
    {
        using var db = NewDb();
        var handler = new CreateRoleCommandHandler(db);

        var response = await handler.Handle(new CreateRoleRequest { Name = "Auditor" }, CancellationToken.None);

        Assert.Equal("Auditor", response.Name);
        Assert.NotEqual(Guid.Empty, response.Id);

        var stored = await db.Roles.FindAsync(response.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsSystemRole);
        Assert.Equal("Auditor", stored.Name);
    }

    [Fact]
    public async Task UpdateRole_ExistingRole_RenamesAndPersists()
    {
        using var db = NewDb();
        var role = new Role { Name = "Old", IsSystemRole = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(db);
        var response = await handler.Handle(
            new UpdateRoleRequest { Id = role.Id, Name = "New" },
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(role.Id, response!.Id);
        Assert.Equal("New", response.Name);

        var reloaded = await db.Roles.FindAsync(role.Id);
        Assert.Equal("New", reloaded!.Name);
    }

    [Fact]
    public async Task UpdateRole_MissingRole_ReturnsNull()
    {
        using var db = NewDb();
        var handler = new UpdateRoleCommandHandler(db);

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
    public async Task DeleteRole_NullInvalidator_StillDeletes()
    {
        using var db = NewDb();
        var role = new Role { Name = "ToDelete2", IsSystemRole = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var handler = new DeleteRoleCommandHandler(db, null);
        var deleted = await handler.Handle(new DeleteRoleRequest { Id = role.Id }, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await db.Roles.FindAsync(role.Id));
    }

    [Fact]
    public async Task DeleteRole_MissingRole_ReturnsFalse()
    {
        using var db = NewDb();
        var invalidator = new Mock<IPermissionCacheInvalidator>(MockBehavior.Strict);
        var handler = new DeleteRoleCommandHandler(db, invalidator.Object);

        var deleted = await handler.Handle(new DeleteRoleRequest { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(deleted);
        invalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetRoleById_Existing_ReturnsResponse()
    {
        using var db = NewDb();
        var role = new Role { Name = "Reader", IsSystemRole = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var handler = new GetRoleByIdQueryHandler(db);
        var response = await handler.Handle(new GetRoleByIdRequest { Id = role.Id }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(role.Id, response!.Id);
        Assert.Equal("Reader", response.Name);
        Assert.True(response.IsSystemRole);
    }

    [Fact]
    public async Task GetRoleById_Missing_ReturnsNull()
    {
        using var db = NewDb();
        var handler = new GetRoleByIdQueryHandler(db);

        var response = await handler.Handle(new GetRoleByIdRequest { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task GetRoles_PaginatesOrderedByName()
    {
        using var db = NewDb();
        db.Roles.AddRange(
            new Role { Name = "Charlie", IsSystemRole = false },
            new Role { Name = "Alpha",   IsSystemRole = true  },
            new Role { Name = "Bravo",   IsSystemRole = false });
        await db.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(db);

        var firstPage = await handler.Handle(new GetRolesRequest { Page = 1, PageSize = 2 }, CancellationToken.None);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(ExpectedFirstPageNames, firstPage.Items.Select(i => i.Name).ToArray());

        var secondPage = await handler.Handle(new GetRolesRequest { Page = 2, PageSize = 2 }, CancellationToken.None);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.Equal("Charlie", secondPage.Items[0].Name);
    }

    [Fact]
    public async Task GetRoles_EmptyDb_ReturnsZeroAndEmptyList()
    {
        using var db = NewDb();
        var handler = new GetRolesQueryHandler(db);

        var response = await handler.Handle(new GetRolesRequest(), CancellationToken.None);

        Assert.Equal(0, response.TotalCount);
        Assert.Empty(response.Items);
    }
}
