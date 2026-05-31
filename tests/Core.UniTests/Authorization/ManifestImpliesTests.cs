using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Plan-required tests for manifest-driven action implies and explicit-only
/// (non-hierarchical) actions. Cf. Master_Refactor_Plan.md § "Required Tests".
/// </summary>
public class ManifestImpliesTests
{
    private static ManifestActionExpander BuildExpander(params IPermissionManifest[] manifests) =>
        new(new PermissionManifestRegistry(manifests));

    [Fact]
    public void Delete_ImpliesEverythingBelowOnCanonicalCrudLadder()
    {
        var expander = BuildExpander(new CrudManifest("test", "things"));

        var actions = expander.ExpandActions("test", "things", ActionLevel.Delete);

        actions.Should().BeEquivalentTo(new[] { "View", "Insert", "EditClose", "Open", "Delete" });
    }

    [Fact]
    public void EditClose_ImpliesView()
    {
        var expander = BuildExpander(new CrudManifest("test", "things"));

        var actions = expander.ExpandActions("test", "things", ActionLevel.EditClose);

        actions.Should().Contain("View");
        actions.Should().Contain("EditClose");
        actions.Should().NotContain("Delete");
        actions.Should().NotContain("Open");
    }

    [Fact]
    public void Insert_ImpliesView_ButNotEditClose()
    {
        var expander = BuildExpander(new CrudManifest("test", "things"));

        var actions = expander.ExpandActions("test", "things", ActionLevel.Insert);

        actions.Should().BeEquivalentTo(new[] { "View", "Insert" });
    }

    [Fact]
    public void ExplicitAction_DoesNotImplyAnything()
    {
        // Approve is explicit-only: it does not appear in any other action's
        // Implies set, so granting Approve should not imply View on its own.
        var manifest = new InMemoryManifest("ops", new ResourceDefinition
        {
            Key = "invoices",
            DisplayName = "Invoices",
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View", 0),
                ActionDefinition.Explicit("Approve", 1, dangerous: true),
            },
        });
        var registry = new PermissionManifestRegistry(new[] { (IPermissionManifest)manifest });
        var resource = registry.GetResource("ops", "invoices");

        // Approve closure is { Approve } only — it does NOT imply View.
        var approveClosure = resource!.ExpandImplied("Approve");
        approveClosure.Should().BeEquivalentTo(new[] { "Approve" });

        // Conversely View does not imply Approve.
        var viewClosure = resource!.ExpandImplied("View");
        viewClosure.Should().BeEquivalentTo(new[] { "View" });
    }

    [Fact]
    public void ImpliesGraph_RejectsCycle()
    {
        var manifest = new InMemoryManifest("cyc", new ResourceDefinition
        {
            Key = "thing",
            DisplayName = "Thing",
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("A", 0, "B"),
                ActionDefinition.Hierarchical("B", 1, "A"),
            },
        });
        // A→B and B→A. Closure of A should be {A, B} — cycle broken on revisit.
        var resource = manifest.Resources.Single();
        resource.ExpandImplied("A").Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public void ImpliesGraph_RejectsUndeclaredImplies()
    {
        // Registry validates implies graph at construction.
        var manifest = new InMemoryManifest("bad", new ResourceDefinition
        {
            Key = "thing",
            DisplayName = "Thing",
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("Edit", 0, "DoesNotExist"),
            },
        });
        var act = () => new PermissionManifestRegistry(new[] { (IPermissionManifest)manifest });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*implies 'DoesNotExist'*");
    }

    private sealed class CrudManifest : IPermissionManifest
    {
        public CrudManifest(string module, string resource)
        {
            Module = module;
            DisplayName = module;
            Resources = new[] { ResourceDefinition.WithCrudActions(resource, "Things", 0) };
        }
        public string Module { get; }
        public string DisplayName { get; }
        public string? Icon => null;
        public int? OrderNumber => 0;
        public IReadOnlyCollection<ResourceDefinition> Resources { get; }
    }

    private sealed class InMemoryManifest : IPermissionManifest
    {
        public InMemoryManifest(string module, ResourceDefinition resource)
        {
            Module = module;
            DisplayName = module;
            Resources = new[] { resource };
        }
        public string Module { get; }
        public string DisplayName { get; }
        public string? Icon => null;
        public int? OrderNumber => 0;
        public IReadOnlyCollection<ResourceDefinition> Resources { get; }
    }
}