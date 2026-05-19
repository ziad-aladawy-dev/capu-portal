using System;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;

/// <summary>
/// Marker request for the global permission-tree query — no inputs (current
/// user is taken from the ambient request context). Record so it stays a
/// distinct type for the handler dispatch without tripping S2094.
/// </summary>
public sealed record GetPermissionTreeRequest;

public class GetRolePermissionsRequest
{
    public Guid RoleId { get; set; }
}
