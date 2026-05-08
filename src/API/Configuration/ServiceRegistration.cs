using Microsoft.Extensions.DependencyInjection;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.CrossCutting.Security;

namespace CapitalUniversity.API.Configuration;

public static class ServiceRegistration
{
    public static void AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IPermissionManagementService, PermissionManagementService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IScopeResolver, ScopeResolver>();
        services.AddScoped<IAuthorizationEvaluator, AuthorizationEvaluator>();
    }
}
