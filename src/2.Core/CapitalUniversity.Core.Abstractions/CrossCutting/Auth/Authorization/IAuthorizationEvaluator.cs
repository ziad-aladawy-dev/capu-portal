using CapitalUniversity.Core.Abstractions;
using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization;

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
    Guid? UniversityId { get; }
    Guid? FacultyId { get; }
    Guid? ProgramId { get; }
    string Year { get; }
    string Semester { get; }
    OverrideType Type { get; }
}

public interface IUserRoleAssignment
{
    Guid RoleId { get; }
    Guid? UniversityId { get; }
    Guid? FacultyId { get; }
    Guid? ProgramId { get; }
    string Year { get; }
    string Semester { get; }
}

public interface IRolePermission
{
    Guid RoleId { get; }
    string Resource { get; }
    ActionLevel Level { get; }
}
