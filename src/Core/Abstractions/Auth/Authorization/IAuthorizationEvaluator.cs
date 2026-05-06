using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.Auth.Authorization;

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

public interface IUserPermissionOverride
{
    Guid Id { get; }
    string Resource { get; }
    ActionLevel Level { get; }
    string Domain { get; }
    string Year { get; }
    string Semester { get; }
    OverrideType Type { get; }
}

public interface IUserRoleAssignment
{
    Guid RoleId { get; }
    string Domain { get; }
    string Year { get; }
    string Semester { get; }
}

public interface IRolePermission
{
    Guid RoleId { get; }
    string Resource { get; }
    ActionLevel Level { get; }
}
