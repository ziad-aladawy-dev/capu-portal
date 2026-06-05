using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using StaffEntity = CapitalUniversity.Core.Domain.Identity.Staff;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Host.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Recipient resolution + content for <see cref="CoreSyncOutcomeNotifier"/>.
/// "Who gets a sync outcome notification" is the permission graph, not a role:
/// every ACTIVE staff member whose role carries any action on the <c>sync</c>
/// resource (the SyncPermissionManifest surface) — nobody else.
/// </summary>
public class CoreSyncOutcomeNotifierTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("SyncNotifier_" + Guid.NewGuid())
            .Options);

    private sealed record SeedIds(Guid SyncResourceId, Guid RoleRecipientId);

    /// <summary>
    /// Seeds the "sync" module/resource plus an unrelated "users" resource, and
    /// returns the sync resource id + the id of the one active staff member who
    /// gets access via a ROLE grant. Also seeds: an inactive sync-permission
    /// holder (excluded by IsActive) and a staff member who only holds the
    /// unrelated permission (excluded by scope). All grants are at Global scope.
    /// </summary>
    private static SeedIds SeedSyncAccessGraph(CoreDbContext db)
    {
        var syncModule = new Module { Id = Guid.NewGuid(), ModuleKey = "sync", DisplayName = "sync" };
        var syncResource = new Resource { Id = Guid.NewGuid(), Module = syncModule, ModuleId = syncModule.Id, Key = "sync", DisplayName = "sync" };
        var usersModule = new Module { Id = Guid.NewGuid(), ModuleKey = "users", DisplayName = "users" };
        var usersResource = new Resource { Id = Guid.NewGuid(), Module = usersModule, ModuleId = usersModule.Id, Key = "users", DisplayName = "users" };

        var syncRole = new Role { Id = Guid.NewGuid(), Name = "OperationsRole" };
        var usersRole = new Role { Id = Guid.NewGuid(), Name = "RegistrarRole" };

        db.Modules.AddRange(syncModule, usersModule);
        db.Resources.AddRange(syncResource, usersResource);
        db.Roles.AddRange(syncRole, usersRole);
        db.RolePermissions.Add(new RolePermission(syncRole.Id, syncResource.Id, "View"));
        db.RolePermissions.Add(new RolePermission(usersRole.Id, usersResource.Id, "View"));

        var activeSyncAdmin = new StaffEntity { Id = Guid.NewGuid(), Name = "Active Sync Admin", IsActive = true };
        var inactiveSyncAdmin = new StaffEntity { Id = Guid.NewGuid(), Name = "Inactive Sync Admin", IsActive = false };
        var unrelatedStaff = new StaffEntity { Id = Guid.NewGuid(), Name = "Registrar", IsActive = true };
        db.Staffs.AddRange(activeSyncAdmin, inactiveSyncAdmin, unrelatedStaff);

        db.StaffRoles.Add(new StaffRoleAssignment(activeSyncAdmin.Id, syncRole.Id, "Global", "Global"));
        db.StaffRoles.Add(new StaffRoleAssignment(inactiveSyncAdmin.Id, syncRole.Id, "Global", "Global"));
        db.StaffRoles.Add(new StaffRoleAssignment(unrelatedStaff.Id, usersRole.Id, "Global", "Global"));

        db.SaveChanges();
        return new SeedIds(syncResource.Id, activeSyncAdmin.Id);
    }

    private static CoreSyncOutcomeNotifier Sut(CoreDbContext db) =>
        new(db, NullLogger<CoreSyncOutcomeNotifier>.Instance);

    [Fact]
    public async Task NotifyAsync_Success_NotifiesOnlyActiveSyncPermissionHolders_WithInfoAndCounts()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
        var seed = SeedSyncAccessGraph(db);

        await Sut(db).NotifyAsync(
            new SyncOutcomeNotice(Guid.NewGuid(), "Student", SyncDirection.Pull,
                Success: true, RecordsProcessed: 42, RecordsFailed: 1, Error: null),
            CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        notifications.Should().ContainSingle("only the one active staff member holding the sync permission is a recipient");
        var n = notifications.Single();
        n.RecipientUserId.Should().Be(seed.RoleRecipientId);
        n.Type.Should().Be(NotificationType.Info);
        n.IsRead.Should().BeFalse();
        n.Message.Should().Contain("Student").And.Contain("42").And.Contain("Pull",
            "the success payload reports the module, processed count and direction");
        n.Title.Should().Contain("\"ar\"").And.Contain("\"en\"", "title is bilingual LocalizedJson");
    }

    [Fact]
    public async Task NotifyAsync_Failure_EmitsWarningCarryingTheError()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
        SeedSyncAccessGraph(db);

        await Sut(db).NotifyAsync(
            new SyncOutcomeNotice(Guid.NewGuid(), "Finance", SyncDirection.Push,
                Success: false, RecordsProcessed: 0, RecordsFailed: 0, Error: "Upstream timeout"),
            CancellationToken.None);

        var n = (await db.Notifications.ToListAsync()).Should().ContainSingle().Subject;
        n.Type.Should().Be(NotificationType.Warning, "terminal failure is a warning (the Error level was retired)");
        n.Message.Should().Contain("Finance").And.Contain("Upstream timeout");
    }

    [Fact]
    public async Task NotifyAsync_NoSyncPermissionHolders_WritesNothing()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
        // Intentionally no seeding: nobody holds the sync permission.

        await Sut(db).NotifyAsync(
            new SyncOutcomeNotice(Guid.NewGuid(), "Courses", SyncDirection.Pull,
                Success: true, RecordsProcessed: 5, RecordsFailed: 0, Error: null),
            CancellationToken.None);

        (await db.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NotifyAsync_AllowOverride_GrantsAccessWithoutARole()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
        var seed = SeedSyncAccessGraph(db);

        // A staff member with NO role assignment, but an explicit Allow override
        // on the sync resource at the same Global scope — must be a recipient.
        var overrideOnly = new StaffEntity { Id = Guid.NewGuid(), Name = "Override Only", IsActive = true };
        db.Staffs.Add(overrideOnly);
        db.StaffPermissions.Add(new StaffPermissionOverride(
            overrideOnly.Id, seed.SyncResourceId, "View", OverrideType.Allow, "Global", "Global"));
        db.SaveChanges();

        await Sut(db).NotifyAsync(
            new SyncOutcomeNotice(Guid.NewGuid(), "Student", SyncDirection.Pull,
                Success: true, RecordsProcessed: 1, RecordsFailed: 0, Error: null),
            CancellationToken.None);

        var recipients = (await db.Notifications.ToListAsync()).Select(x => x.RecipientUserId).ToList();
        recipients.Should().BeEquivalentTo(new[] { seed.RoleRecipientId, overrideOnly.Id },
            "the role holder AND the Allow-override holder both get notified");
    }

    [Fact]
    public async Task NotifyAsync_DenyOverride_RevokesAMatchingRoleGrant()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
        var seed = SeedSyncAccessGraph(db);

        // Deny the role holder's sync View at the SAME scope+action as their role
        // grant — effective = allow − deny leaves them with nothing, so the only
        // active sync-permission holder drops out and nobody is notified.
        db.StaffPermissions.Add(new StaffPermissionOverride(
            seed.RoleRecipientId, seed.SyncResourceId, "View", OverrideType.Deny, "Global", "Global"));
        db.SaveChanges();

        await Sut(db).NotifyAsync(
            new SyncOutcomeNotice(Guid.NewGuid(), "Student", SyncDirection.Pull,
                Success: true, RecordsProcessed: 1, RecordsFailed: 0, Error: null),
            CancellationToken.None);

        (await db.Notifications.CountAsync()).Should().Be(0,
            "a Deny override at the matching scope+action cancels the role grant");
    }

    [Fact]
    public async Task NotifyAsync_ExpiredAllowOverride_IsIgnored()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
        var seed = SeedSyncAccessGraph(db);

        var expiredHolder = new StaffEntity { Id = Guid.NewGuid(), Name = "Expired Grant", IsActive = true };
        db.Staffs.Add(expiredHolder);
        var expired = new StaffPermissionOverride(
            expiredHolder.Id, seed.SyncResourceId, "View", OverrideType.Allow, "Global", "Global")
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
        };
        db.StaffPermissions.Add(expired);
        db.SaveChanges();

        await Sut(db).NotifyAsync(
            new SyncOutcomeNotice(Guid.NewGuid(), "Student", SyncDirection.Pull,
                Success: true, RecordsProcessed: 1, RecordsFailed: 0, Error: null),
            CancellationToken.None);

        var recipients = (await db.Notifications.ToListAsync()).Select(x => x.RecipientUserId).ToList();
        recipients.Should().ContainSingle().Which.Should().Be(seed.RoleRecipientId,
            "an expired Allow override no longer grants access");
    }
}
