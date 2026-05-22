using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Domain.Authorization;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Raw permission data access for a single user inside a single scope. Two
/// flavours: <see cref="GetAllPermissionsAsync"/> loads every grant the user
/// touches (used to build the cached full lookup), and
/// <see cref="GetResourcePermissionsAsync"/> narrows to a single resource key
/// when a callsite only needs that slice. Use the resource-scoped form
/// whenever the caller knows the resource — it avoids hauling rows for
/// unrelated resources back through EF.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Loads every override, role assignment, and role permission row that
    /// applies to <paramref name="userId"/> under <paramref name="scope"/>,
    /// across every resource. Used by the cached lookup builder.
    /// </summary>
    Task<PermissionLoadResult> GetAllPermissionsAsync(
        Guid userId,
        AuthorizationScope? scope = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Narrower variant restricted to a single resource <paramref name="resourceKey"/>.
    /// </summary>
    Task<PermissionLoadResult> GetResourcePermissionsAsync(
        Guid userId,
        string resourceKey,
        AuthorizationScope? scope = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bundle returned by <see cref="IPermissionService"/>. Splitting the three
/// streams out as a record keeps callsites explicit about what they read.
/// </summary>
public sealed record PermissionLoadResult(
    IEnumerable<IUserPermissionOverride> Overrides,
    IEnumerable<IUserRoleAssignment> Assignments,
    IEnumerable<IRolePermission> RolePermissions);
