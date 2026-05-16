namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Module-owned declaration of every permission the module needs. Implementations
/// are discovered via DI (register every implementation as <c>IPermissionManifest</c>)
/// and aggregated by <see cref="IPermissionManifestRegistry"/>; the
/// <see cref="IPermissionManifestSynchronizer"/> reconciles the aggregated set into
/// the database <c>Modules</c> + <c>Services</c> tables on startup.
///
/// <para>
/// Each module is responsible for its own permissions — no scattered seeder code,
/// no orphaned <c>[HasPermission(...)]</c> literals. The contract test against
/// <c>PermissionNames</c> remains the build-time guard that <c>[HasPermission]</c>
/// values exist somewhere structured; the manifest adds the runtime guarantee that
/// the database row backing each value actually exists.
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

    /// <summary>Every permission this module owns. <c>(Resource, Action)</c> pairs must be unique within the collection.</summary>
    IReadOnlyCollection<PermissionDefinition> Permissions { get; }
}
