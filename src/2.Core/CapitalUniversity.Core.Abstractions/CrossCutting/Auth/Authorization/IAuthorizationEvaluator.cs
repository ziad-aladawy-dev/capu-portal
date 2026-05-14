using CapitalUniversity.Core.Abstractions;
using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.Shared;

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

public interface IUserPermissionOverride
{
    Guid Id { get; }
    string Resource { get; }
    ActionLevel Level { get; }
    Guid? StructureNodeId { get; }
    string? StructureNodePath { get; }
    string Year { get; }
    string Semester { get; }
    OverrideType Type { get; }
}

public interface IUserRoleAssignment
{
    Guid RoleId { get; }
    Guid? StructureNodeId { get; }
    string? StructureNodePath { get; }
    string Year { get; }
    string Semester { get; }
}

public interface IRolePermission
{
    Guid RoleId { get; }
    string Resource { get; }
    ActionLevel Level { get; }
}
