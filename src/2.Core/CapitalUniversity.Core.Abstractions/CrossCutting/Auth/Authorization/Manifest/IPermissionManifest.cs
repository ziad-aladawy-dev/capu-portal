namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Module-owned declaration of every resource (and its action verbs) the module
/// gates. Discovered via DI (register every implementation as
/// <c>IPermissionManifest</c>), aggregated by
/// <see cref="IPermissionManifestRegistry"/>, and reconciled into the
/// <c>Modules</c> + <c>Resources</c> tables on startup by
/// <see cref="IPermissionManifestSynchronizer"/>.
///
/// <para>
/// Authoring shape is <c>Module → Resources → Actions</c>. Each module declares
/// the resources it owns; each resource lists the action verbs it supports. The
/// canonical identity used by <c>[HasPermission]</c> attributes is
/// <c>{Module}.{ResourceKey}.{Action}</c>. The contract test against
/// <c>PermissionNames</c> remains the build-time guard that
/// <c>[HasPermission]</c> values exist somewhere structured; the manifest adds
/// the runtime guarantee that the database row backing each value actually
/// exists.
/// </para>
/// </summary>
public interface IPermissionManifest
{
    /// <summary>Module key (e.g., <c>"academics"</c>, <c>"permissions"</c>, <c>"notifications"</c>). Unique across the registry.</summary>
    string Module { get; }

    /// <summary>Human-readable name used when seeding a fresh <c>Module</c> row.</summary>
    string DisplayName { get; }

    /// <summary>Icon hint for the UI when seeding a fresh <c>Module</c> row. Optional.</summary>
    string? Icon { get; }

    /// <summary>Ordering inside the parent UI listing. Optional; defaults to end-of-list.</summary>
    int? OrderNumber { get; }

    /// <summary>
    /// Every resource this module owns, each with its action verbs. <c>Key</c>
    /// values must be unique within this collection.
    /// </summary>
    IReadOnlyCollection<ResourceDefinition> Resources { get; }
}
