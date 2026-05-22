using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;

/// <summary>
/// Bridges the legacy <see cref="ActionLevel"/> DTO shape and the manifest-driven
/// action model. Single source of truth for level↔action translation:
/// <list type="bullet">
///   <item><see cref="ExpandActions"/> — write path. Translates an incoming
///         <c>Level</c> grant into the set of action names that should be
///         persisted as per-action rows.</item>
///   <item><see cref="CollapseToMaxLevel"/> — read path. Translates a stored
///         action set back into the highest legacy ladder step it fully covers,
///         so existing API consumers keep seeing the <c>Level</c> shape.</item>
/// </list>
/// </summary>
public sealed class ManifestActionExpander
{
    private readonly IPermissionManifestRegistry _registry;

    public ManifestActionExpander(IPermissionManifestRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Maps the legacy <see cref="ActionLevel"/> enum to the canonical action
    /// name used by the manifest. Returns <c>null</c> for <see cref="ActionLevel.None"/>.
    /// </summary>
    public static string? LevelToActionName(ActionLevel level) => level switch
    {
        ActionLevel.View      => "View",
        ActionLevel.Insert    => "Insert",
        ActionLevel.EditClose => "EditClose",
        ActionLevel.Open      => "Open",
        ActionLevel.Delete    => "Delete",
        _ => null,
    };

    public static ActionLevel ActionNameToLevel(string action) => action switch
    {
        "View"      => ActionLevel.View,
        "Insert"    => ActionLevel.Insert,
        "EditClose" => ActionLevel.EditClose,
        "Open"      => ActionLevel.Open,
        "Delete"    => ActionLevel.Delete,
        _ => ActionLevel.None,
    };

    /// <summary>
    /// Returns the set of action names a grant of <paramref name="level"/> on
    /// the given module+resource covers, after expansion via
    /// <see cref="ActionDefinition.Implies"/>. Unknown resources or unmapped
    /// levels yield an empty set. Use this for <b>allow</b> grants.
    /// </summary>
    public IReadOnlySet<string> ExpandActions(string module, string resourceKey, ActionLevel level)
        => ExpandActionsCore(module, resourceKey, level, forDeny: false);

    /// <summary>
    /// Like <see cref="ExpandActions"/> but expands through the <i>reverse</i>
    /// implies graph — for <b>deny</b> overrides. Denying <c>EditClose</c>
    /// returns <c>{EditClose, Open, Delete}</c> on the canonical CRUD ladder
    /// because <c>Open</c> and <c>Delete</c> would otherwise grant <c>EditClose</c>
    /// transitively. Symmetric to the legacy ladder semantic (deny X removes X
    /// and above) but expressed via the manifest's per-resource graph.
    /// </summary>
    public IReadOnlySet<string> ExpandDenyActions(string module, string resourceKey, ActionLevel level)
        => ExpandActionsCore(module, resourceKey, level, forDeny: true);

    private IReadOnlySet<string> ExpandActionsCore(string module, string resourceKey, ActionLevel level, bool forDeny)
    {
        var actionName = LevelToActionName(level);
        if (actionName is null) return Empty;

        var resource = _registry.GetResource(module, resourceKey);
        if (resource is null) return Empty;

        var declaredNames = resource.Actions.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        if (declaredNames.Contains(actionName))
        {
            return forDeny ? resource.ExpandReverseImplied(actionName) : resource.ExpandImplied(actionName);
        }

        // Legacy fallback: the requested Level names an action this resource
        // does not declare. Walk down the ladder to the highest declared.
        for (var lvl = (int)level - 1; lvl > 0; lvl--)
        {
            var candidate = LevelToActionName((ActionLevel)lvl);
            if (candidate is not null && declaredNames.Contains(candidate))
            {
                return forDeny ? resource.ExpandReverseImplied(candidate) : resource.ExpandImplied(candidate);
            }
        }
        return Empty;
    }

    /// <summary>
    /// Inverse of <see cref="ExpandActions"/>: given a stored set of action
    /// names for a resource, returns the highest legacy <see cref="ActionLevel"/>
    /// step <c>L</c> such that the resource's expansion of <c>L</c> is a subset
    /// of the stored set. Used to emit the legacy <c>Level</c> on read-side
    /// DTOs.
    /// </summary>
    public ActionLevel CollapseToMaxLevel(string module, string resourceKey, IReadOnlyCollection<string> storedActions)
    {
        if (storedActions.Count == 0) return ActionLevel.None;

        var stored = new HashSet<string>(storedActions, StringComparer.Ordinal);
        var resource = _registry.GetResource(module, resourceKey);
        if (resource is null)
        {
            // Resource not declared — best effort: pick the max stored ladder name.
            ActionLevel best = ActionLevel.None;
            foreach (var a in storedActions)
            {
                var lvl = ActionNameToLevel(a);
                if (lvl > best) best = lvl;
            }
            return best;
        }

        for (var lvl = (int)ActionLevel.Delete; lvl > 0; lvl--)
        {
            var name = LevelToActionName((ActionLevel)lvl);
            if (name is null) continue;
            var expanded = resource.ExpandImplied(name);
            if (expanded.Count == 0) continue;
            if (expanded.IsSubsetOf(stored)) return (ActionLevel)lvl;
        }
        return ActionLevel.None;
    }

    public static readonly IReadOnlySet<string> Empty = new HashSet<string>();
}
