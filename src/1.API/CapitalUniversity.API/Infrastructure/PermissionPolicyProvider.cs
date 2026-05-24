using System.Collections.Concurrent;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.API.Infrastructure;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    // L6 — Policy cache. Policy names are a bounded domain (one per
    // PermissionNames constant, optionally with a scope-binding suffix), so
    // a process-lifetime ConcurrentDictionary is a strict win over rebuilding
    // the AuthorizationPolicy + PermissionRequirement objects per request.
    // No eviction needed.
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policyCache = new(StringComparer.OrdinalIgnoreCase);

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => FallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionIdentity.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return FallbackPolicyProvider.GetPolicyAsync(policyName);
        }

        if (_policyCache.TryGetValue(policyName, out var cached))
        {
            return Task.FromResult<AuthorizationPolicy?>(cached);
        }

        // C3 — split scope binding off the permission part. Format:
        //   Permission:Module.Resource.Action
        //   Permission:Module.Resource.Action|ScopeKind:routeParamName
        var pipe = policyName.IndexOf('|');
        var permissionSegment = pipe < 0 ? policyName : policyName[..pipe];
        var scopeSegment = pipe < 0 ? null : policyName[(pipe + 1)..];

        if (!PermissionIdentity.TryParse(permissionSegment, out var module, out var resource, out var action))
        {
            return FallbackPolicyProvider.GetPolicyAsync(policyName);
        }

        var canonicalPermission = PermissionIdentity.Create(module, resource, action);
        var (scopeKind, scopeRouteValue) = ParseScope(scopeSegment);

        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(canonicalPermission, scopeKind, scopeRouteValue))
            .Build();

        // L6 — race-tolerant put; if two threads build the same policy
        // concurrently, GetOrAdd returns the first one stored.
        var stored = _policyCache.GetOrAdd(policyName, policy);
        return Task.FromResult<AuthorizationPolicy?>(stored);
    }

    private static (PermissionScopeKind, string?) ParseScope(string? scopeSegment)
    {
        if (string.IsNullOrWhiteSpace(scopeSegment))
        {
            return (PermissionScopeKind.None, null);
        }

        var colon = scopeSegment.IndexOf(':');
        if (colon < 0)
        {
            return (PermissionScopeKind.None, null);
        }

        var kindText = scopeSegment[..colon];
        var routeName = scopeSegment[(colon + 1)..];

        if (!Enum.TryParse<PermissionScopeKind>(kindText, ignoreCase: true, out var kind) || kind == PermissionScopeKind.None)
        {
            return (PermissionScopeKind.None, null);
        }

        return (kind, string.IsNullOrWhiteSpace(routeName) ? null : routeName);
    }
}
