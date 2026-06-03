using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

/// <summary>
/// Per-resource action grant for a role, in the canonical manifest model.
/// </summary>
public class RoleResourcePermissionsModel
{
    public Guid ResourceId { get; set; }
    public List<string> Actions { get; set; } = new();
}

/// <summary>
/// Sets a role's permissions. The request is the <b>complete desired</b> permission
/// set for the role (full-replace): resources/actions not present are removed.
/// </summary>
public class SetRolePermissionsRequest
{
    public Guid RoleId { get; set; }
    public List<RoleResourcePermissionsModel> Resources { get; set; } = new();
}

public class SetRolePermissionsResponse
{
    public Guid RoleId { get; set; }
    public List<RoleResourcePermissionsModel> Resources { get; set; } = new();
}

/// <summary>
/// Replaces a role's per-action <see cref="RolePermission"/> rows. Each requested
/// action is expanded through the resource manifest's forward implies graph (a role
/// grant is an allow), so storage stays closure-complete and consistent with the
/// override write path. A role-permission change rotates the role's cache version so
/// every staff member holding it rebuilds on the next lookup.
/// </summary>
public class SetRolePermissionsCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionManagementService _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly ManifestActionExpander _expander;
    private readonly IPermissionCacheInvalidator? _cacheInvalidator;

    public SetRolePermissionsCommandHandler(
        CoreDbContext dbContext,
        IPermissionManagementService permissions,
        ICurrentUser currentUser,
        ManifestActionExpander expander,
        IPermissionCacheInvalidator? cacheInvalidator = null)
    {
        _dbContext = dbContext;
        _permissions = permissions;
        _currentUser = currentUser;
        _expander = expander;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<SetRolePermissionsResponse?> Handle(SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        // M7 — defence-in-depth; see CreateRoleCommandHandler for rationale.
        await EnsureCanManageRolesAsync(PermissionNames.Roles.EditClose, cancellationToken);

        var role = await _dbContext.Roles.FindAsync(new object[] { request.RoleId }, cancellationToken);
        if (role is null) return null;

        var resourceIds = request.Resources.Select(r => r.ResourceId).Distinct().ToList();
        var resourceById = await _dbContext.Resources
            .Include(r => r.Module)
            .Where(r => resourceIds.Contains(r.Id))
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        // Desired closure-complete grant set: forward implies per requested action.
        var desired = new HashSet<(Guid ResourceId, string Action)>();
        foreach (var resModel in request.Resources)
        {
            if (!resourceById.TryGetValue(resModel.ResourceId, out var resource)) continue;
            foreach (var action in resModel.Actions)
            {
                if (string.IsNullOrWhiteSpace(action)) continue;
                foreach (var expanded in _expander.ExpandActionNames(resource.Module.ModuleKey, resource.Key, action))
                {
                    desired.Add((resModel.ResourceId, expanded));
                }
            }
        }

        // Full-replace reconcile against the role's current rows.
        var current = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .ToListAsync(cancellationToken);

        foreach (var row in current.Where(rp => !desired.Contains((rp.ResourceId, rp.Action))))
        {
            _dbContext.RolePermissions.Remove(row);
        }

        var currentSet = current.Select(rp => (rp.ResourceId, rp.Action)).ToHashSet();
        foreach (var (resourceId, action) in desired.Where(d => !currentSet.Contains(d)))
        {
            _dbContext.RolePermissions.Add(new RolePermission(request.RoleId, resourceId, action));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Role-grant change → every staff member holding the role must rebuild.
        if (_cacheInvalidator is not null)
        {
            await _cacheInvalidator.InvalidateRoleAsync(request.RoleId, cancellationToken);
        }

        var stored = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new SetRolePermissionsResponse
        {
            RoleId = request.RoleId,
            Resources = stored
                .GroupBy(rp => rp.ResourceId)
                .Select(g => new RoleResourcePermissionsModel
                {
                    ResourceId = g.Key,
                    Actions = g.Select(rp => rp.Action).Distinct(StringComparer.Ordinal).ToList(),
                })
                .ToList(),
        };
    }

    private async Task EnsureCanManageRolesAsync(string permission, CancellationToken cancellationToken)
    {
        if (_currentUser.Id == Guid.Empty) return;
        var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
        if (!grants.Contains(PermissionIdentity.Parse(permission)))
        {
            throw new ForbiddenException(LocalizedKeys.Permissions.Forbidden);
        }
    }
}
