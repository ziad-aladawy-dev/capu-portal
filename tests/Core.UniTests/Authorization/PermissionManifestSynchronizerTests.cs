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
///   - Empty DB → creates one Module + one Resource per manifest-declared resource.
///   - Run twice → second pass is a no-op.
///   - Pre-existing module / resource rows are left intact (teammate-seeded data
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
        public IReadOnlyCollection<ResourceDefinition> Resources { get; init; } = Array.Empty<ResourceDefinition>();
    }

    [Fact]
    public async Task EmptyDb_CreatesModuleAndResourcesPerManifest()
    {
        using var db = NewDb();
        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Resources = new[]
                {
                    ResourceDefinition.Of("things", "Things", 0, "View", "Insert"),
                    ResourceDefinition.Of("widgets", "Widgets", 1, "View"),
                }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        var report = await sut.SynchronizeAsync();

        report.ModulesCreated.Should().Be(1);
        report.ResourcesCreated.Should().Be(2);
        report.ManifestsProcessed.Should().Be(1);

        (await db.Modules.AsNoTracking().SingleAsync()).ModuleKey.Should().Be("alpha");
        var resources = await db.Resources.AsNoTracking().ToListAsync();
        resources.Select(r => r.Key).Should().BeEquivalentTo(new[] { "things", "widgets" });
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
                Resources = new[] { ResourceDefinition.Of("things", "Things", 0, "View") }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        await sut.SynchronizeAsync();
        var second = await sut.SynchronizeAsync();

        second.ModulesCreated.Should().Be(0);
        second.ResourcesCreated.Should().Be(0);
        (await db.Modules.CountAsync()).Should().Be(1);
        (await db.Resources.CountAsync()).Should().Be(1);
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
        db.Resources.Add(new Resource
        {
            Id = Guid.NewGuid(),
            ModuleId = teammateModuleId,
            Key = "structure",
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
                Resources = new[] { ResourceDefinition.Of("things", "Things", 0, "View") }
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
    public async Task ExistingResourceWithSameKey_RefreshesMetadataButPreservesIdentity()
    {
        using var db = NewDb();

        // Pretend the legacy seeder already wrote the row we'd otherwise create.
        var existingModuleId = Guid.NewGuid();
        var existingResourceId = Guid.NewGuid();
        db.Modules.Add(new Module
        {
            Id = existingModuleId,
            ModuleKey = "alpha",
            DisplayName = "Alpha (legacy display)",
        });
        db.Resources.Add(new Resource
        {
            Id = existingResourceId,
            ModuleId = existingModuleId,
            Key = "things",
            DisplayName = "Things (legacy display)",
            OrderNumber = 42
        });
        await db.SaveChangesAsync();

        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha (manifest display)",
                Resources = new[] { ResourceDefinition.Of("things", "Things (manifest display)", 0, "View") }
            }
        });

        var sut = new PermissionManifestSynchronizer(db, registry);
        var report = await sut.SynchronizeAsync();

        // No new rows — these existed already and matched by natural key.
        report.ModulesCreated.Should().Be(0);
        report.ResourcesCreated.Should().Be(0);

        // Identity (Id, ModuleKey, ResourceKey) is preserved so FKs stay valid.
        var module = await db.Modules.AsNoTracking().SingleAsync();
        module.Id.Should().Be(existingModuleId, "natural-key match must reuse the existing row");
        module.ModuleKey.Should().Be("alpha");
        // Display metadata is refreshed so a rename in code propagates to the UI
        // without orphaning historical assignments.
        module.DisplayName.Should().Be("Alpha (manifest display)",
            "the synchroniser must refresh DisplayName on existing rows so renames in code propagate");

        var resource = await db.Resources.AsNoTracking().SingleAsync();
        resource.Id.Should().Be(existingResourceId, "the pre-existing row should remain in place");
        resource.Key.Should().Be("things");
        resource.DisplayName.Should().Be("Things (manifest display)",
            "the synchroniser must refresh resource DisplayName on existing rows");
        resource.OrderNumber.Should().Be(0,
            "the synchroniser must refresh OrderNumber on existing rows so re-ordering propagates");
    }
}