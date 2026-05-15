using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Domain.Authorization;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public interface IPermissionService
{
    Task<(IEnumerable<IUserPermissionOverride> Overrides, IEnumerable<IUserRoleAssignment> Assignments, IEnumerable<IRolePermission> RolePermissions)> GetPermissionsAsync(
        Guid userId,
        string resource,
        AuthorizationScope? scope = null,
        CancellationToken cancellationToken = default);
}
