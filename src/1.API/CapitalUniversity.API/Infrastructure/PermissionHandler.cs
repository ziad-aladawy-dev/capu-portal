using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CapitalUniversity.API.Infrastructure;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionManagementService _permissionService;
    private readonly ICurrentUser _currentUser;

    public PermissionHandler(IPermissionManagementService permissionService, ICurrentUser currentUser)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_currentUser.Id == Guid.Empty)
        {
            return;
        }

        // Optimized hashtable lookup (HashSet)
        var permissions = await _permissionService.GetPermissionLookupAsync(_currentUser.Id);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
