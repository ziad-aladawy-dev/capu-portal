using Microsoft.Extensions.DependencyInjection;
using CapitalUniversity.Core.Application.Auth.Authorization.Roles.Commands.CreateRole;
using CapitalUniversity.Core.Application.Auth.Authorization.Roles.Commands.UpdateRole;
using CapitalUniversity.Core.Application.Auth.Authorization.Roles.Commands.DeleteRole;
using CapitalUniversity.Core.Application.Auth.Authorization.Roles.Queries.GetRoleById;
using CapitalUniversity.Core.Application.Auth.Authorization.Roles.Queries.GetRoles;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.CrossCutting.Security;

namespace CapitalUniversity.API.Configuration;

public static class ServiceRegistration
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IPermissionManagementService, PermissionManagementService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationEvaluator, AuthorizationEvaluator>();
        services.AddScoped<IScopeResolver, ScopeResolver>();

        services.AddRoleHandlers();
        return services;
    }

    public static IServiceCollection AddRoleHandlers(this IServiceCollection services)
    {
        services.AddScoped<CreateRoleCommandHandler>();
        services.AddScoped<UpdateRoleCommandHandler>();
        services.AddScoped<DeleteRoleCommandHandler>();
        services.AddScoped<GetRoleByIdQueryHandler>();
        services.AddScoped<GetRolesQueryHandler>();
        return services;
    }
}
