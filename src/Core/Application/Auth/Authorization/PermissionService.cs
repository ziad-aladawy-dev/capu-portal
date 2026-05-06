using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.CrossCutting.Security;

public class PermissionService : IPermissionService
{
    public Task<(IEnumerable<IUserPermissionOverride> Overrides, IEnumerable<IUserRoleAssignment> Assignments, IEnumerable<IRolePermission> RolePermissions)> GetPermissionsAsync(
        Guid userId,
        string resource,
        AuthorizationScope scope,
        CancellationToken cancellationToken = default)
    {
        // Simulated Db fetch (in real scenario this comes from repositories/Db)
        var userOverrides = new List<IUserPermissionOverride>();
        var userAssignments = new List<IUserRoleAssignment>();
        var rolePermissions = new List<IRolePermission>();

        return Task.FromResult<(IEnumerable<IUserPermissionOverride>, IEnumerable<IUserRoleAssignment>, IEnumerable<IRolePermission>)>(
            (userOverrides, userAssignments, rolePermissions)
        );
    }
}
