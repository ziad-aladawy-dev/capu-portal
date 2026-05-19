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
        AuthorizationScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(userId, scope, cancellationToken);
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();

        var rolePermissions = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId) && (resource == "*" || rp.Resource == resource))
            .ToListAsync(cancellationToken);

        var overrides = await LoadOverridesAsync(userId, resource, scope, cancellationToken);

        return (overrides.Cast<IUserPermissionOverride>(), assignments.Cast<IUserRoleAssignment>(), rolePermissions.Cast<IRolePermission>());
    }

    private Task<List<StaffRoleAssignment>> LoadAssignmentsAsync(Guid userId, AuthorizationScope? scope, CancellationToken cancellationToken)
    {
        var query = _dbContext.StaffRoles.Where(sr => sr.StaffId == userId);
        if (scope is not null)
        {
            // Predicate stays inline so EF can translate it to SQL — every
            // axis is either Global (unrestricted) or must match the active
            // scope; structural path matches when the grant's path is a
            // prefix of the active path.
            query = query.Where(sr =>
                (sr.Year == ScopeKeys.Global || sr.Year == scope.Year) &&
                (sr.Semester == ScopeKeys.Global || sr.Semester == scope.Semester) &&
                (sr.StructureNodePath == null
                    || (scope.StructureNodePath != null && scope.StructureNodePath.StartsWith(sr.StructureNodePath))));
        }
        return query.ToListAsync(cancellationToken);
    }

    private Task<List<StaffPermissionOverride>> LoadOverridesAsync(Guid userId, string resource, AuthorizationScope? scope, CancellationToken cancellationToken)
    {
        var query = _dbContext.StaffPermissions.Where(sp => sp.StaffId == userId && (resource == "*" || sp.Resource == resource));
        if (scope is not null)
        {
            query = query.Where(sp =>
                (sp.Year == ScopeKeys.Global || sp.Year == scope.Year) &&
                (sp.Semester == ScopeKeys.Global || sp.Semester == scope.Semester) &&
                (sp.StructureNodePath == null
                    || (scope.StructureNodePath != null && scope.StructureNodePath.StartsWith(sp.StructureNodePath))));
        }
        return query.ToListAsync(cancellationToken);
    }
}
