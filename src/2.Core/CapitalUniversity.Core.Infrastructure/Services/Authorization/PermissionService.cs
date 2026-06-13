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

    public Task<PermissionLoadResult> GetAllPermissionsAsync(
        Guid userId,
        AuthorizationScope? scope = null,
        CancellationToken cancellationToken = default) =>
        LoadAsync(userId, resourceKey: null, scope, cancellationToken);

    public Task<PermissionLoadResult> GetResourcePermissionsAsync(
        Guid userId,
        string resourceKey,
        AuthorizationScope? scope = null,
        CancellationToken cancellationToken = default) =>
        LoadAsync(userId, resourceKey, scope, cancellationToken);

    private async Task<PermissionLoadResult> LoadAsync(
        Guid userId,
        string? resourceKey,
        AuthorizationScope? scope,
        CancellationToken cancellationToken)
    {
        var assignments = await LoadAssignmentsAsync(userId, scope, cancellationToken);
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();

        var rolePermissions = await _dbContext.RolePermissions
            .Include(rp => rp.Resource)
                .ThenInclude(r => r.Module)
            .Where(rp => roleIds.Contains(rp.RoleId) && (resourceKey == null || rp.Resource.Key == resourceKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var overrides = await LoadOverridesAsync(userId, resourceKey, scope, cancellationToken);

        return new PermissionLoadResult(
            overrides.Cast<IUserPermissionOverride>(),
            assignments.Cast<IUserRoleAssignment>(),
            rolePermissions.Cast<IRolePermission>());
    }

    private Task<List<StaffRoleAssignment>> LoadAssignmentsAsync(Guid userId, AuthorizationScope? scope, CancellationToken cancellationToken)
    {
        var query = _dbContext.StaffRoles.AsNoTracking().Where(sr => sr.StaffId == userId);
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

    private Task<List<StaffPermissionOverride>> LoadOverridesAsync(Guid userId, string? resourceKey, AuthorizationScope? scope, CancellationToken cancellationToken)
    {
        // Temporal expiry: a stamped ExpiresAt that is now-or-past means the
        // override's temporal window has closed, so it must not grant or deny
        // anything — exclude it here even before the manual ExpireOverridesAsync
        // sweep physically removes the row. Null ExpiresAt = Global/never-expires.
        var now = DateTime.UtcNow;
        var query = _dbContext.StaffPermissions
            .Include(sp => sp.Resource)
                .ThenInclude(r => r.Module)
            .AsNoTracking()
            .Where(sp => sp.StaffId == userId
                && (resourceKey == null || sp.Resource.Key == resourceKey)
                && (sp.ExpiresAt == null || sp.ExpiresAt > now));
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
