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
    public string Module => "permissions";
    public string DisplayName => "Permissions & Roles";
    public string? Icon => "Shield";
    public int? OrderNumber => 4;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        // ── Permissions sub-resource ───────────────────────────────────────
        PermissionDefinition.Create("permissions", "View",      "View Permissions",   0),
        PermissionDefinition.Create("permissions", "Insert",    "Create Permissions", 1),
        PermissionDefinition.Create("permissions", "EditClose", "Manage Permissions", 2),
        PermissionDefinition.Create("permissions", "Open",      "Open Permissions",   3),
        PermissionDefinition.Create("permissions", "Delete",    "Delete Permissions", 4),

        // ── Roles sub-resource ─────────────────────────────────────────────
        PermissionDefinition.Create("roles", "View",      "View Roles",   5),
        PermissionDefinition.Create("roles", "Insert",    "Create Roles", 6),
        PermissionDefinition.Create("roles", "EditClose", "Manage Roles", 7),
        PermissionDefinition.Create("roles", "Open",      "Open Roles",   8),
        PermissionDefinition.Create("roles", "Delete",    "Delete Roles", 9),
    };
}
