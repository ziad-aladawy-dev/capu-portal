using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CreateRoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Keeping it simple as a service/handler for now, if MediatR is used we can adapt.
public class CreateRoleCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly ILocalizationService _localization;
    private readonly IPermissionManagementService _permissions;
    private readonly ICurrentUser _currentUser;

    public CreateRoleCommandHandler(
        CoreDbContext dbContext,
        ILocalizationService localization,
        IPermissionManagementService permissions,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _localization = localization;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<CreateRoleResponse> Handle(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        // M7 — defence-in-depth. The controller already requires
        // PermissionNames.Roles.Insert, but the handler is callable from any
        // in-process caller (background jobs, future internal dispatchers).
        // Anonymous/system contexts skip the check so trusted background work
        // (e.g. seeding) is not blocked.
        await EnsureCanManageRolesAsync(PermissionNames.Roles.Insert, cancellationToken);

        var role = new Role
        {
            Name = LocalizedJson.Normalize(request.Name),
            IsSystemRole = false // Custom roles
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateRoleResponse
        {
            Id = role.Id,
            Name = _localization.Get<string>(role.Name)
        };
    }

    private async Task EnsureCanManageRolesAsync(string permission, CancellationToken cancellationToken)
    {
        if (_currentUser.Id == Guid.Empty) return;
        var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
        if (!grants.Contains(permission))
        {
            throw new ForbiddenException(LocalizedKeys.Permissions.Forbidden);
        }
    }
}
