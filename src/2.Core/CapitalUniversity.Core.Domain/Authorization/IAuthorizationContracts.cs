using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Authorization;

public interface IUserPermissionOverride
{
    Guid Id { get; }
    Guid ResourceId { get; }
    string Action { get; }
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
    Guid ResourceId { get; }
    string Action { get; }
}
