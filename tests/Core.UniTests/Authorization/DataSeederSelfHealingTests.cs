using CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class DataSeederSelfHealingTests
{
    private static CoreDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new CoreDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task SeedAsync_FromCleanDb_PopulatesAllCanonicalRows()
    {
        var ctx = NewDb();
        var hasher = new PasswordHasher();

        await DataSeeder.SeedAsync(ctx, hasher);

        Assert.Equal(8, await ctx.Staffs.CountAsync());
        Assert.Equal(17, await ctx.Students.CountAsync());
        Assert.Equal(8, await ctx.StaffRoles.CountAsync());
        Assert.True(await ctx.RolePermissions.AnyAsync(), "RolePermissions must be populated.");

        // Khaled exists and authenticates with admin123.
        var khaled = await ctx.Staffs.FirstOrDefaultAsync(s => s.NationalId == "28102021234567");
        Assert.NotNull(khaled);
        Assert.True(hasher.VerifyHashedPassword(khaled!.PasswordHash, "admin123"));
    }

    [Fact]
    public async Task SeedAsync_FromPartiallyPopulatedStaff_ConvergesToFullSet()
    {
        // Reproduce the user's broken state: 3 stale staff that don't include Khaled.
        var ctx = NewDb();
        var hasher = new PasswordHasher();

        // Pre-seed the prerequisites the upsert needs (structure, years, etc.) via the
        // one-shot seeders first by running SeedAsync once on a clean DB, then artificially
        // mangle Staffs to mimic the broken state and run SeedAsync again.
        await DataSeeder.SeedAsync(ctx, hasher);

        // Wipe everything Staff-dependent and reduce Staffs to 3 unrelated rows.
        ctx.StaffRoles.RemoveRange(ctx.StaffRoles);
        ctx.Notifications.RemoveRange(ctx.Notifications);
        ctx.Staffs.RemoveRange(ctx.Staffs);
        await ctx.SaveChangesAsync();

        var anyNode = await ctx.StructureNodes.FirstAsync();
        ctx.Staffs.AddRange(
            new Staff { Id = Guid.NewGuid(), EmployeeCode = "STALE-1", Name = "Stale One",   NationalId = "00000000000001", PasswordHash = "x", StructureNodeId = anyNode.Id, IsActive = true },
            new Staff { Id = Guid.NewGuid(), EmployeeCode = "STALE-2", Name = "Stale Two",   NationalId = "00000000000002", PasswordHash = "x", StructureNodeId = anyNode.Id, IsActive = true },
            new Staff { Id = Guid.NewGuid(), EmployeeCode = "STALE-3", Name = "Stale Three", NationalId = "00000000000003", PasswordHash = "x", StructureNodeId = anyNode.Id, IsActive = true }
        );
        await ctx.SaveChangesAsync();
        Assert.Equal(3, await ctx.Staffs.CountAsync());
        Assert.Equal(0, await ctx.StaffRoles.CountAsync());

        // Re-run the seeder — this is what should happen on the next API startup.
        await DataSeeder.SeedAsync(ctx, hasher);

        // 8 canonical + 3 stale = 11; Khaled must now exist with the right password.
        Assert.Equal(11, await ctx.Staffs.CountAsync());
        var khaled = await ctx.Staffs.FirstOrDefaultAsync(s => s.NationalId == "28102021234567");
        Assert.NotNull(khaled);
        Assert.True(hasher.VerifyHashedPassword(khaled!.PasswordHash, "admin123"));

        // StaffRoles must rebuild — line 419 should not throw, missing staff are skipped.
        Assert.Equal(8, await ctx.StaffRoles.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_WithPartiallyPopulatedRolePermissions_DoesNotViolateUniqueIndex()
    {
        // Reproduces the SqlException 2601 duplicate-key crash:
        // a previous seed attempt left RolePermissions in the DB, and the next
        // run uses a fresh DbContext whose .Local is empty. The upsert must read
        // the DB, not just .Local, otherwise it tries to re-insert duplicates.
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var hasher = new PasswordHasher();

        // First context: full seed.
        int fullCount;
        using (var ctx1 = new CoreDbContext(options))
        {
            ctx1.Database.EnsureCreated();
            await DataSeeder.SeedAsync(ctx1, hasher);
            fullCount = await ctx1.RolePermissions.CountAsync();
            Assert.True(fullCount > 0);
        }

        // Second context shares the same in-memory store but has empty .Local —
        // exactly the production startup scenario after a prior partial seed.
        using (var ctx2 = new CoreDbContext(options))
        {
            await DataSeeder.SeedAsync(ctx2, hasher);
            Assert.Equal(fullCount, await ctx2.RolePermissions.CountAsync());
        }
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        var ctx = NewDb();
        var hasher = new PasswordHasher();

        await DataSeeder.SeedAsync(ctx, hasher);
        var staffCount1 = await ctx.Staffs.CountAsync();
        var rolePermsCount1 = await ctx.RolePermissions.CountAsync();
        var staffRolesCount1 = await ctx.StaffRoles.CountAsync();

        await DataSeeder.SeedAsync(ctx, hasher);
        var staffCount2 = await ctx.Staffs.CountAsync();
        var rolePermsCount2 = await ctx.RolePermissions.CountAsync();
        var staffRolesCount2 = await ctx.StaffRoles.CountAsync();

        Assert.Equal(staffCount1, staffCount2);
        Assert.Equal(rolePermsCount1, rolePermsCount2);
        Assert.Equal(staffRolesCount1, staffRolesCount2);
    }
}
