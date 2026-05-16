using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Caching;

/// <summary>
/// Invalidator contract:
///   - Per-user rotation writes a fresh Guid stamp at <c>perm_lookup_v_{userId}</c>.
///   - Per-role rotation rotates the stamp for every current assignee of that role.
///   - Global rotation writes a fresh Guid at the epoch key.
/// </summary>
public class PermissionCacheInvalidatorTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("PermInvalidator_" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task InvalidateUserAsync_WritesNewVersionStamp()
    {
        var cache = new Mock<ICacheService>();
        var userId = Guid.NewGuid();
        using var db = NewDb();
        var sut = new PermissionCacheInvalidator(cache.Object, db, Options.Create(new PermissionCacheOptions()));

        await sut.InvalidateUserAsync(userId);

        cache.Verify(c => c.SetAsync(
            $"perm_lookup_v_{userId}",
            It.Is<string>(v => !string.IsNullOrEmpty(v)),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateRoleAsync_RotatesStampForEveryAssignee()
    {
        var cache = new Mock<ICacheService>();
        using var db = NewDb();
        var roleId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var staffB = Guid.NewGuid();
        var staffOther = Guid.NewGuid();

        db.StaffRoles.AddRange(
            new StaffRoleAssignment(staffA,     roleId,           "Global", "Global"),
            new StaffRoleAssignment(staffB,     roleId,           "Global", "Global"),
            new StaffRoleAssignment(staffOther, Guid.NewGuid(),   "Global", "Global"));
        await db.SaveChangesAsync();

        var sut = new PermissionCacheInvalidator(cache.Object, db, Options.Create(new PermissionCacheOptions()));

        await sut.InvalidateRoleAsync(roleId);

        cache.Verify(c => c.SetAsync(
            $"perm_lookup_v_{staffA}", It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        cache.Verify(c => c.SetAsync(
            $"perm_lookup_v_{staffB}", It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        cache.Verify(c => c.SetAsync(
            $"perm_lookup_v_{staffOther}", It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "users not holding the rotated role must not be invalidated");
    }

    [Fact]
    public async Task InvalidateRoleAsync_NoAssignees_IsNoOp()
    {
        var cache = new Mock<ICacheService>();
        using var db = NewDb();
        var sut = new PermissionCacheInvalidator(cache.Object, db, Options.Create(new PermissionCacheOptions()));

        await sut.InvalidateRoleAsync(Guid.NewGuid());

        cache.Verify(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvalidateAllAsync_RotatesGlobalEpochKey()
    {
        var cache = new Mock<ICacheService>();
        using var db = NewDb();
        var sut = new PermissionCacheInvalidator(cache.Object, db, Options.Create(new PermissionCacheOptions()));

        await sut.InvalidateAllAsync();

        cache.Verify(c => c.SetAsync(
            PermissionCacheInvalidator.GlobalEpochKey,
            It.Is<string>(v => !string.IsNullOrEmpty(v)),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
