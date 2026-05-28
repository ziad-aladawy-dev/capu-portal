using System;
using System.Diagnostics.CodeAnalysis;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;

/// <summary>
/// Marker request for the global permission-tree query — no inputs (current
/// user is taken from the ambient request context). The type stays empty by
/// design so handler dispatch can distinguish it from <see cref="GetRolePermissionsRequest"/>.
/// </summary>
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "Marker request type for handler dispatch — intentionally empty.")]
public sealed record GetPermissionTreeRequest;

public class GetRolePermissionsRequest
{
    public Guid RoleId { get; set; }
}

public class GetUserPermissionTreeRequest
{
    public Guid UserId { get; set; }
}
