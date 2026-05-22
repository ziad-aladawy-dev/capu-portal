using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Plan-required test: <i>deny overrides implied allow</i>. Under the
/// per-action storage model, a deny on a sub-action must transitively
/// invalidate every action that implies it — otherwise a user granted
/// <c>Delete</c> (which forward-implies <c>EditClose</c>) would still hold
/// <c>Delete</c> after the admin denies <c>EditClose</c>, silently failing
/// open. Reverse-implies expansion fixes that.
/// </summary>
public class DenyTransitivityTests
{
    private static ManifestActionExpander Build(string module = "test", string resource = "things") =>
        new(new PermissionManifestRegistry(new[]
        {
            (IPermissionManifest)new CrudManifest(module, resource),
        }));

    [Fact]
    public void DenyExpansion_OnEditClose_AlsoCoversOpenAndDelete()
    {
        var expander = Build();

        var deniedActions = expander.ExpandDenyActions("test", "things", ActionLevel.EditClose);

        deniedActions.Should().BeEquivalentTo(new[] { "EditClose", "Open", "Delete" },
            "denying EditClose must transitively block every verb that would otherwise grant it");
    }

    [Fact]
    public void DenyExpansion_OnView_BlocksEveryCrudVerb()
    {
        var expander = Build();

        var deniedActions = expander.ExpandDenyActions("test", "things", ActionLevel.View);

        // Every CRUD verb implies View, so denying View revokes all of them.
        deniedActions.Should().BeEquivalentTo(new[] { "View", "Insert", "EditClose", "Open", "Delete" });
    }

    [Fact]
    public void DenyExpansion_OnDelete_OnlyCoversDelete()
    {
        var expander = Build();

        var deniedActions = expander.ExpandDenyActions("test", "things", ActionLevel.Delete);

        // Nothing implies Delete (top of the ladder) so the closure is just {Delete}.
        deniedActions.Should().BeEquivalentTo(new[] { "Delete" });
    }

    [Fact]
    public void AllowExpansion_RemainsForwardDirected()
    {
        // Sanity guard so deny refactor didn't accidentally invert allow too.
        var expander = Build();

        var allowed = expander.ExpandActions("test", "things", ActionLevel.EditClose);

        allowed.Should().BeEquivalentTo(new[] { "View", "Insert", "EditClose" });
    }

    [Fact]
    public void DeleteGrant_MinusEditCloseDeny_LeavesOnlyViewAndInsert()
    {
        // Worked example matching the audit's worry: user has Delete via role
        // grant, admin issues a deny override on EditClose. Effective set
        // should be {View, Insert} — Delete and Open are removed because they
        // would otherwise re-grant EditClose.
        var expander = Build();

        var allowed = expander.ExpandActions("test", "things", ActionLevel.Delete);
        var denied = expander.ExpandDenyActions("test", "things", ActionLevel.EditClose);

        var effective = new HashSet<string>(allowed);
        effective.ExceptWith(denied);

        effective.Should().BeEquivalentTo(new[] { "View", "Insert" });
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
}
