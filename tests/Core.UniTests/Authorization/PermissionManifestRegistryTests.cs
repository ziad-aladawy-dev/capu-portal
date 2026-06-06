using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.Courses.Authorization;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Notifications.Authorization;
using CapitalUniversity.Core.Application.Semesters.Authorization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Registry construction is the validation surface — duplicate module keys,
/// duplicate resource keys within a module, empty fields, and duplicate
/// canonical names all surface as a startup-time throw so the rest of the
/// system can trust the snapshot.
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
        public IReadOnlyCollection<ResourceDefinition> Resources { get; init; } = Array.Empty<ResourceDefinition>();
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
                Resources = new[]
                {
                    ResourceDefinition.Of("x", "X", 0, "View", "Insert"),
                }
            },
            new StubManifest
            {
                Module = "beta",
                DisplayName = "Beta",
                Resources = new[] { ResourceDefinition.Of("y", "Y", 0, "View") }
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
    public void Build_DuplicateResourceKeyInSameManifest_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Resources = new[]
                {
                    ResourceDefinition.Of("x", "X (first)",  0, "View"),
                    ResourceDefinition.Of("x", "X (second)", 0, "Insert"),
                }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate resource key 'x'*");
    }

    [Fact]
    public void Build_DuplicateActionOnSameResource_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Resources = new[] { ResourceDefinition.Of("x", "X", 0, "View", "View") }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate action 'View'*");
    }

    [Fact]
    public void Build_EmptyResourceKey_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Resources = new[] { ResourceDefinition.Of(string.Empty, "X", 0, "View") }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty Key*");
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
                Resources = new[] { ResourceDefinition.Of("x", string.Empty, 0, "View") }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty DisplayName*");
    }

    [Fact]
    public void Build_ResourceWithNoActions_Throws()
    {
        var manifests = new IPermissionManifest[]
        {
            new StubManifest
            {
                Module = "alpha",
                DisplayName = "Alpha",
                Resources = new[] { ResourceDefinition.Of("x", "X", 0) }
            }
        };

        Action build = () => _ = new PermissionManifestRegistry(manifests);
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares no Actions*");
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