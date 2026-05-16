using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Synchroniser is additive + idempotent:
///   - Empty DB → creates one Module + N Service rows per manifest.
///   - Run twice → second pass is a no-op.
///   - Pre-existing module / service rows are left intact (teammate-seeded data
///     must not be disturbed).
/// </summary>
public class PermissionManifestSynchronizerTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("ManifestSync_" + Guid.NewGuid())
            .Options);

    private sealed class StubManifest : IPermissionManifest
    {
        public string Module { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int? OrderNumber { get; init; }
        public IReadOnlyCollection<PermissionDefinition> Permissions { get; init; } = Array.Empty<PermissionDefinition>();
    }

    [Fact]
    public async Task EmptyDb_CreatesModuleAndServicesPerManifest()
    {
        using var db = NewDb();
        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[]
                {
                    PermissionDefinition.Create("things", "View",   "View Things"),
                    PermissionDefinition.Create("things", "Insert", "Create Things"),
                }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        var report = await sut.SynchronizeAsync();

        report.ModulesCreated.Should().Be(1);
        report.ServicesCreated.Should().Be(2);
        report.ManifestsProcessed.Should().Be(1);

        (await db.Modules.AsNoTracking().SingleAsync()).ModuleKey.Should().Be("alpha");
        var services = await db.Services.AsNoTracking().ToListAsync();
        services.Select(s => s.DisplayName).Should().BeEquivalentTo(new[] { "View Things", "Create Things" });
    }

    [Fact]
    public async Task SecondRun_IsIdempotent()
    {
        using var db = NewDb();
        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[] { PermissionDefinition.Create("things", "View", "View Things") }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        await sut.SynchronizeAsync();
        var second = await sut.SynchronizeAsync();

        second.ModulesCreated.Should().Be(0);
        second.ServicesCreated.Should().Be(0);
        (await db.Modules.CountAsync()).Should().Be(1);
        (await db.Services.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PreExistingTeammateRows_AreNotTouched()
    {
        using var db = NewDb();

        // Simulate a teammate-seeded module the manifest knows nothing about.
        var teammateModuleId = Guid.NewGuid();
        db.Modules.Add(new Module
        {
            Id = teammateModuleId,
            ModuleKey = "structure",
            DisplayName = "University Structure",
            Icon = "Building2",
            OrderNumber = 2
        });
        db.Services.Add(new Service
        {
            Id = Guid.NewGuid(),
            ModuleId = teammateModuleId,
            DisplayName = "Manage Structure",
            OrderNumber = 1
        });
        await db.SaveChangesAsync();

        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[] { PermissionDefinition.Create("things", "View", "View Things") }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        await sut.SynchronizeAsync();

        var modules = await db.Modules.AsNoTracking().ToListAsync();
        modules.Should().Contain(m => m.ModuleKey == "structure" && m.DisplayName == "University Structure",
            "the synchroniser must never touch teammate-owned rows");
        modules.Should().Contain(m => m.ModuleKey == "alpha");
        modules.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExistingServiceWithSameDisplayName_IsLeftIntact()
    {
        using var db = NewDb();

        // Pretend the legacy seeder already wrote the row we'd otherwise create.
        var existingModuleId = Guid.NewGuid();
        var existingServiceId = Guid.NewGuid();
        db.Modules.Add(new Module
        {
            Id = existingModuleId,
            ModuleKey = "alpha",
            DisplayName = "Alpha (legacy display)",
        });
        db.Services.Add(new Service
        {
            Id = existingServiceId,
            ModuleId = existingModuleId,
            DisplayName = "View Things",
            OrderNumber = 42
        });
        await db.SaveChangesAsync();

        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha (manifest display)",
                Permissions = new[] { PermissionDefinition.Create("things", "View", "View Things") }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        var report = await sut.SynchronizeAsync();

        report.ModulesCreated.Should().Be(0);
        report.ServicesCreated.Should().Be(0);

        var module = await db.Modules.AsNoTracking().SingleAsync();
        module.DisplayName.Should().Be("Alpha (legacy display)",
            "additive sync must not rename rows that already match the natural key");

        var svc = await db.Services.AsNoTracking().SingleAsync();
        svc.Id.Should().Be(existingServiceId, "the pre-existing row should remain in place");
        svc.OrderNumber.Should().Be(42, "manifest-supplied OrderNumber must not overwrite legacy values");
    }
}
