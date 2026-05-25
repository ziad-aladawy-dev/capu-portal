using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Mappings;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

public class UpdateRoleRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

public class UpdateRoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UpdateRoleCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly ILocalizationService _localization;
    private readonly IPermissionManagementService _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly RoleMapper _mapper = new();

    public UpdateRoleCommandHandler(
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

    public async Task<UpdateRoleResponse?> Handle(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        // M7 — defence-in-depth; see CreateRoleCommandHandler for rationale.
        if (_currentUser.Id != Guid.Empty)
        {
            var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
            // Canonicalise — see CreateRoleCommandHandler for rationale.
            if (!grants.Contains(PermissionIdentity.Parse(PermissionNames.Roles.EditClose)))
            {
                throw new ForbiddenException(LocalizedKeys.Permissions.Forbidden);
            }
        }

        var role = await _dbContext.Roles.FindAsync(new object[] { request.Id }, cancellationToken);

        if (role == null) return null;

        if (request.Name != null) role.Name = LocalizedJson.Normalize(request.Name);
        _mapper.ApplyUpdate(request, role);
        role.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateRoleResponse
        {
            Id = role.Id,
            Name = _localization.Get<string>(role.Name)
        };

    }
}
