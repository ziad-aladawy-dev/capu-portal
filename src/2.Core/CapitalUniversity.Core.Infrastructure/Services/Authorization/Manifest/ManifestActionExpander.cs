using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;

/// <summary>
/// Expands per-resource action grants through the manifest's implies graph — the
/// single source of truth for action inheritance. Allow grants expand <b>forward</b>
/// (granting a verb grants everything it implies); Deny overrides expand <b>reverse</b>
/// (denying a verb denies everything that would grant it transitively). Operates on
/// canonical action names; the legacy <c>ActionLevel</c> ladder is gone.
/// </summary>
public sealed class ManifestActionExpander
{
    private readonly IPermissionManifestRegistry _registry;

    public ManifestActionExpander(IPermissionManifestRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Forward implies closure for a single action name — the action plus everything it
    /// transitively implies. Unknown resource/action yields an empty set. For <b>allow</b> grants.
    /// </summary>
    public IReadOnlySet<string> ExpandActionNames(string module, string resourceKey, string action)
    {
        var resource = _registry.GetResource(module, resourceKey);
        return resource is null ? Empty : resource.ExpandImplied(action);
    }

    /// <summary>
    /// Reverse implies closure for a single action name — the action plus every action that
    /// transitively implies it. For <b>deny</b> overrides, so denying a low verb also denies
    /// every verb that would re-grant it.
    /// </summary>
    public IReadOnlySet<string> ExpandDenyActionNames(string module, string resourceKey, string action)
    {
        var resource = _registry.GetResource(module, resourceKey);
        return resource is null ? Empty : resource.ExpandReverseImplied(action);
    }

    public static readonly IReadOnlySet<string> Empty = new HashSet<string>();
}
