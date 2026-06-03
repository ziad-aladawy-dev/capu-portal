using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
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

        var actions = expander.ExpandActionNames("test", "things", "Delete");

        actions.Should().BeEquivalentTo(new[] { "View", "Insert", "EditClose", "Open", "Delete" });
    }

    [Fact]
    public void EditClose_ImpliesView()
    {
        var expander = BuildExpander(new CrudManifest("test", "things"));

        var actions = expander.ExpandActionNames("test", "things", "EditClose");

        actions.Should().Contain("View");
        actions.Should().Contain("EditClose");
        actions.Should().NotContain("Delete");
        actions.Should().NotContain("Open");
    }

    [Fact]
    public void Insert_ImpliesView_ButNotEditClose()
    {
        var expander = BuildExpander(new CrudManifest("test", "things"));

        var actions = expander.ExpandActionNames("test", "things", "Insert");

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

    // ---------------------------------------------------------------------
    // Three-deep TRANSITIVE implies chain (each verb declares only the next
    // hop, not the full set). EditClose -> Insert -> View. Confirms the closure
    // walks all three hops for both the forward (allow) and reverse (deny)
    // directions on a chained graph.
    // ---------------------------------------------------------------------

    private static IPermissionManifest ChainedCrud(string module, string resource) =>
        new InMemoryManifest(module, new ResourceDefinition
        {
            Key = resource,
            DisplayName = "Chain",
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View", 0),
                ActionDefinition.Hierarchical("Insert", 1, "View"),        // -> View
                ActionDefinition.Hierarchical("EditClose", 2, "Insert"),   // -> Insert (NOT View directly)
            },
        });

    [Fact]
    public void ChainedImplies_ThreeDeep_AllowExpandsTransitively_ViaActionNames()
    {
        var expander = BuildExpander(ChainedCrud("test", "chain"));

        // Granting EditClose must walk EditClose -> Insert -> View.
        var actions = expander.ExpandActionNames("test", "chain", "EditClose");

        actions.Should().BeEquivalentTo(new[] { "View", "Insert", "EditClose" });
    }

    [Fact]
    public void ChainedImplies_ThreeDeep_DenyExpandsReverseTransitively_ViaActionNames()
    {
        var expander = BuildExpander(ChainedCrud("test", "chain"));

        // Denying the lowest verb (View) must remove every verb that transitively
        // grants it: Insert (->View) and EditClose (->Insert->View).
        var actions = expander.ExpandDenyActionNames("test", "chain", "View");

        actions.Should().BeEquivalentTo(new[] { "View", "Insert", "EditClose" });
    }

    [Fact]
    public void ChainedImplies_MiddleVerb_AllowGoesDown_DenyGoesUp()
    {
        var expander = BuildExpander(ChainedCrud("test", "chain"));

        // Allow Insert -> {Insert, View} (one hop down), never EditClose.
        expander.ExpandActionNames("test", "chain", "Insert")
            .Should().BeEquivalentTo(new[] { "View", "Insert" });

        // Deny Insert -> {Insert, EditClose} (one hop up), never View.
        expander.ExpandDenyActionNames("test", "chain", "Insert")
            .Should().BeEquivalentTo(new[] { "Insert", "EditClose" });
    }

    [Fact]
    public void ChainedImplies_NonCrudVerbs_ExpandTransitively_BothDirections()
    {
        // Same three-deep shape with non-CRUD names (Publish -> Approve -> Draft),
        // proving transitivity is a property of the graph, not the CRUD ladder.
        var manifest = new InMemoryManifest("ops", new ResourceDefinition
        {
            Key = "articles",
            DisplayName = "Articles",
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("Draft", 0),
                ActionDefinition.Hierarchical("Approve", 1, "Draft"),
                ActionDefinition.Hierarchical("Publish", 2, "Approve"),
            },
        });
        var resource = new PermissionManifestRegistry(new[] { (IPermissionManifest)manifest })
            .GetResource("ops", "articles")!;

        resource.ExpandImplied("Publish").Should().BeEquivalentTo(new[] { "Publish", "Approve", "Draft" });
        resource.ExpandReverseImplied("Draft").Should().BeEquivalentTo(new[] { "Draft", "Approve", "Publish" });
        resource.ExpandReverseImplied("Approve").Should().BeEquivalentTo(new[] { "Approve", "Publish" });
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