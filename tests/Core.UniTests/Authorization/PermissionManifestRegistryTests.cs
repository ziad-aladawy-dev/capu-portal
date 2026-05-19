using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Registry construction is the validation surface — duplicate keys, empty fields,
/// and duplicate canonical names all surface as a startup-time throw, so the rest
/// of the system can trust the snapshot.
/// </summary>
public class PermissionManifestRegistryTests
{
    private static readonly string[] ExpectedCanonicalNames = { "alpha.x.View", "alpha.x.Insert", "beta.y.View" };

    private sealed class StubManifest : IPermissionManifest
    {
        public string Module { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int? OrderNumber { get; init; }
        public IReadOnlyCollection<PermissionDefinition> Permissions { get; init; } = Array.Empty<PermissionDefinition>();
    }

    [Fact]
    public void Build_ValidManifests_AggregatesCanonicalNames()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[]
                {
                    PermissionDefinition.Create("x", "View",   "View X"),
                    PermissionDefinition.Create("x", "Insert", "Create X"),
                }
            },
            new StubManifest
            {
                Module = "beta",
                DisplayName = "Beta",
                Permissions = new[] { PermissionDefinition.Create("y", "View", "View Y") }
            },
        };

        var registry = new PermissionManifestRegistry(manifests);

        registry.Manifests.Should().HaveCount(2);
        registry.AllCanonicalNames.Should().BeEquivalentTo(ExpectedCanonicalNames);
        registry.Contains("alpha.x.View").Should().BeTrue();
        registry.Contains("missing.thing.View").Should().BeFalse();
    }

    [Fact]
    public void Build_DuplicateModuleKey_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest { Module = "shared", DisplayName = "First" },
            new StubManifest { Module = "shared", DisplayName = "Second" },
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate IPermissionManifest module key 'shared'*");
    }

    [Fact]
    public void Build_DuplicateResourceActionInSameManifest_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[]
                {
                    PermissionDefinition.Create("x", "View", "View X (first)"),
                    PermissionDefinition.Create("x", "View", "View X (second)"),
                }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate permission 'x.View'*");
    }

    [Fact]
    public void Build_EmptyAction_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[] { PermissionDefinition.Create("x", string.Empty, "Broken") }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty Action*");
    }

    [Fact]
    public void Build_MissingDisplayName_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Permissions = new[] { PermissionDefinition.Create("x", "View", string.Empty) }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*without a DisplayName*");
    }

    [Fact]
    public void ShippedManifests_LoadCleanly()
    {
        // Belt-and-braces: instantiate the real production manifests through the registry
        // so anyone editing them sees a failure on duplicate names BEFORE shipping.
        var registry = new PermissionManifestRegistry(new IPermissionManifest[]
        {
            new AcademicsPermissionManifest(),
            new AuthorizationPermissionManifest(),
            new NotificationsPermissionManifest(),
        });

        registry.AllCanonicalNames.Should().Contain("academics.academic-years.View")
            .And.Contain("academics.academic-years.Delete")
            .And.Contain("permissions.permissions.View")
            .And.Contain("permissions.roles.View")
            .And.Contain("notifications.notifications.View");
    }
}
