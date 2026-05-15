using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.API.Infrastructure;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => FallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionIdentity.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (PermissionIdentity.TryParse(policyName, out var module, out var resource, out var action))
            {
                var canonicalPermission = PermissionIdentity.Create(module, resource, action);
                var policy = new AuthorizationPolicyBuilder();
                policy.AddRequirements(new PermissionRequirement(canonicalPermission));
                return Task.FromResult<AuthorizationPolicy?>(policy.Build());
            }
        }

        return FallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
