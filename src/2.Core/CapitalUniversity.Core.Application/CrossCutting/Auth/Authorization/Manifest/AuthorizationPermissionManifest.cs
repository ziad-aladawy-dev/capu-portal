using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Owns the <c>permissions</c> module — the meta-module that gates who can see
/// and manage permissions / roles themselves. Two resources:
/// <c>permissions</c> (the catalog) and <c>roles</c> (role definitions), each
/// with the full action quintet.
/// </summary>
public sealed class AuthorizationPermissionManifest : IPermissionManifest
{
    public string Module => "permissions";
    public string DisplayName => "Permissions & Roles";
    public string? Icon => "Shield";
    public int? OrderNumber => 4;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("permissions", "Permissions", 0),
        ResourceDefinition.WithCrudActions("roles",       "Roles",       1),
    };
}
