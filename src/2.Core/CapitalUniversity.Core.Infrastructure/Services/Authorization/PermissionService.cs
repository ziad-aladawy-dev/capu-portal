using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

public class PermissionService : IPermissionService
{
    private readonly CoreDbContext _dbContext;

    public PermissionService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IEnumerable<IUserPermissionOverride> Overrides, IEnumerable<IUserRoleAssignment> Assignments, IEnumerable<IRolePermission> RolePermissions)> GetPermissionsAsync(
        Guid userId,
        string resource,
        AuthorizationScope scope,
        CancellationToken cancellationToken = default)
    {
        // 1. Get assignments that match the scope (hierarchical path matching)
        var assignments = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == userId &&
                         (sr.Year == "Global" || sr.Year == scope.Year) &&
                         (sr.Semester == "Global" || sr.Semester == scope.Semester) &&
                         (sr.StructureNodePath == null || (scope.StructureNodePath != null && scope.StructureNodePath.StartsWith(sr.StructureNodePath))))
            .ToListAsync(cancellationToken);

        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();

        // 2. Get role permissions
        var rolePermissions = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId) && (resource == "*" || rp.Resource == resource))
            .ToListAsync(cancellationToken);

        // 3. Get overrides
        var overrides = await _dbContext.StaffPermissions
            .Where(sp => sp.StaffId == userId &&
                         (sp.Year == "Global" || sp.Year == scope.Year) &&
                         (sp.Semester == "Global" || sp.Semester == scope.Semester) &&
                         (sp.StructureNodePath == null || (scope.StructureNodePath != null && scope.StructureNodePath.StartsWith(sp.StructureNodePath))) &&
                         (resource == "*" || sp.Resource == resource))
            .ToListAsync(cancellationToken);

        return (overrides.Cast<IUserPermissionOverride>(), assignments.Cast<IUserRoleAssignment>(), rolePermissions.Cast<IRolePermission>());
    }
}
