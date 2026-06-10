using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

public class AddRoleMemberRequest
{
    public Guid RoleId { get; set; }
    public Guid StaffId { get; set; }
}

public class AddRoleMemberCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionManagementService _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionCacheInvalidator? _cacheInvalidator;

    public AddRoleMemberCommandHandler(
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

    public async Task<bool> Handle(AddRoleMemberRequest request, CancellationToken cancellationToken)
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

        var staff = await _dbContext.Staffs.FindAsync(new object[] { request.StaffId }, cancellationToken);
        if (staff == null) return false;

        var alreadyAssigned = await _dbContext.StaffRoles
            .AnyAsync(sr => sr.RoleId == request.RoleId
                         && sr.StaffId == request.StaffId
                         && sr.Year == ScopeKeys.Global
                         && sr.Semester == ScopeKeys.Global, cancellationToken);

        if (alreadyAssigned) return true;

        var assignment = new StaffRoleAssignment(request.StaffId, request.RoleId, ScopeKeys.Global, ScopeKeys.Global);
        _dbContext.StaffRoles.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_cacheInvalidator is not null)
        {
            await _cacheInvalidator.InvalidateUserAsync(request.StaffId, cancellationToken);
        }

        return true;
    }
}
