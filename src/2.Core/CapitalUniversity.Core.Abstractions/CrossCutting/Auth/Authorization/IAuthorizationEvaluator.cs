using CapitalUniversity.Core.Abstractions;
using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public interface IAuthorizationEvaluator
{
    AuthorizationResult Evaluate(
        Guid userId,
        string resource,
        ActionLevel requiredLevel,
        bool isClosed,
        AuthorizationScope scope,
        IEnumerable<IUserPermissionOverride> overrides,
        IEnumerable<IUserRoleAssignment> assignments,
        IEnumerable<IRolePermission> rolePermissions);
}
