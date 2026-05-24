using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

public class DeleteRoleRequest
{
    public Guid Id { get; set; }
}

public class DeleteRoleCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionCacheInvalidator? _cacheInvalidator;
    private readonly IPermissionManagementService _permissions;
    private readonly ICurrentUser _currentUser;

    public DeleteRoleCommandHandler(
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

    public async Task<bool> Handle(DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        // M7 — defence-in-depth; see CreateRoleCommandHandler for rationale.
        if (_currentUser.Id != Guid.Empty)
        {
            var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
            if (!grants.Contains(PermissionNames.Roles.Delete))
            {
                throw new ForbiddenException(LocalizedKeys.Permissions.Forbidden);
            }
        }

        var role = await _dbContext.Roles.FindAsync(new object[] { request.Id }, cancellationToken);

        if (role == null) return false;

        // P1.2 — Security Guard: prevent accidental deletion of core system roles
        // (Admin, Faculty, Student, etc.) via API bypass.
        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("System roles are managed by the core platform and cannot be deleted.");
        }

        // Snapshot assignees BEFORE delete so the cascade doesn't drop them from
        // StaffRoles before the invalidator queries.
        if (_cacheInvalidator is not null)
        {
            await _cacheInvalidator.InvalidateRoleAsync(request.Id, cancellationToken);
        }

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
