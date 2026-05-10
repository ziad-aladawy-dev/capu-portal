using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Auth.Authorization.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Core.Application.Auth.Authentication;

public class AuthorizationResponseBuilder : IAuthorizationResponseBuilder
{
    private readonly IServiceProvider _serviceProvider;

    public AuthorizationResponseBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<(AuthorizedScopesDto Scopes, List<PermissionDto> Permissions, ActiveScopeDto ActiveScope)> BuildAsync(IUserCredential user, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionManagementService>();

        var scopes = new AuthorizedScopesDto
        {
            AllowedFacultyIds = new List<Guid>(),
            AllowedProgramIds = new List<Guid>(),
            AllowedAcademicYearIds = new List<Guid>(),
            AllowedSemesterIds = new List<Guid>()
        };

        var activeScope = new ActiveScopeDto
        {
            Structural = new StructuralScopeDto(),
            Temporal = new TemporalScopeDto()
        };

        var permissionDtos = new List<PermissionDto>();

        if (user.Role == "Staff")
        {
            try
            {
                 permissionDtos = await permissionService.GetEffectivePermissionsAsync(user.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                 // Expected for testing/users without mapping setups. Real telemetry goes here.
                 System.Console.WriteLine(ex.Message);
            }
        }

        return (scopes, permissionDtos, activeScope);
    }
}
