using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CapitalUniversity.API.Infrastructure;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    // C3 — When ScopeKind != None, the handler will also verify that the
    // request's route parameter named <see cref="ScopeRouteValue"/> resolves
    // to a resource the caller can access via IEffectiveScope. None preserves
    // pre-C3 behaviour (action-only enforcement).
    public PermissionScopeKind ScopeKind { get; }
    public string? ScopeRouteValue { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
        ScopeKind = PermissionScopeKind.None;
        ScopeRouteValue = null;
    }

    public PermissionRequirement(string permission, PermissionScopeKind scopeKind, string? scopeRouteValue)
    {
        Permission = permission;
        ScopeKind = scopeKind;
        ScopeRouteValue = scopeRouteValue;
    }
}
