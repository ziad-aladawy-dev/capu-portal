using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Owns the <c>permissions</c> module — the meta-module that gates who can see and
/// manage permissions / roles themselves. Resource names match the legacy seeder
/// (<c>permissions</c> and <c>roles</c>) so the values round-trip against existing
/// rows and the existing <c>PermissionNames.Permissions</c>/<c>PermissionNames.Roles</c>
/// constants.
/// </summary>
public sealed class AuthorizationPermissionManifest : IPermissionManifest
{
    private const string ResourcePermissions = "permissions";
    private const string ResourceRoles = "roles";

    public string Module => ResourcePermissions;
    public string DisplayName => "Permissions & Roles";
    public string? Icon => "Shield";
    public int? OrderNumber => 4;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        // ── Permissions sub-resource ───────────────────────────────────────
        PermissionDefinition.Create(ResourcePermissions, "View",      "View Permissions",   0),
        PermissionDefinition.Create(ResourcePermissions, "Insert",    "Create Permissions", 1),
        PermissionDefinition.Create(ResourcePermissions, "EditClose", "Manage Permissions", 2),
        PermissionDefinition.Create(ResourcePermissions, "Open",      "Open Permissions",   3),
        PermissionDefinition.Create(ResourcePermissions, "Delete",    "Delete Permissions", 4),

        // ── Roles sub-resource ─────────────────────────────────────────────
        PermissionDefinition.Create(ResourceRoles, "View",      "View Roles",   5),
        PermissionDefinition.Create(ResourceRoles, "Insert",    "Create Roles", 6),
        PermissionDefinition.Create(ResourceRoles, "EditClose", "Manage Roles", 7),
        PermissionDefinition.Create(ResourceRoles, "Open",      "Open Roles",   8),
        PermissionDefinition.Create(ResourceRoles, "Delete",    "Delete Roles", 9),
    };
}
