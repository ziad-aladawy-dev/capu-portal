using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

public class RemoveRoleMemberRequest
{
    public Guid RoleId { get; set; }
    public Guid StaffId { get; set; }
}

public class RemoveRoleMemberCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionManagementService _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionCacheInvalidator? _cacheInvalidator;

    public RemoveRoleMemberCommandHandler(
        CoreDbContext dbContext,
        IPermissionManagementService permissions,
        ICurrentUser currentUser,
        IPermissionCacheInvalidator? cacheInvalidator = null)
    {
        _dbContext = dbContext;
        _permissions = permissions;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<bool> Handle(RemoveRoleMemberRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.Id != Guid.Empty)
        {
            var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
            if (!grants.Contains(PermissionIdentity.Parse(PermissionNames.Roles.EditClose)))
            {
                throw new ForbiddenException(LocalizedKeys.Permissions.Forbidden);
            }
        }

        var role = await _dbContext.Roles.FindAsync(new object[] { request.RoleId }, cancellationToken);
        if (role == null) return false;

        var assignments = await _dbContext.StaffRoles
            .Where(sr => sr.RoleId == request.RoleId && sr.StaffId == request.StaffId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0) return false;

        foreach (var assignment in assignments)
        {
            _dbContext.StaffRoles.Remove(assignment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_cacheInvalidator is not null)
        {
            await _cacheInvalidator.InvalidateUserAsync(request.StaffId, cancellationToken);
        }

        return true;
    }
}
