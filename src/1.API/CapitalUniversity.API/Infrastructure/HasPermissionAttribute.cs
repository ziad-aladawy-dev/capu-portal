using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CapitalUniversity.API.Infrastructure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    // C3 — Optional scope binding. When set, the handler reads the named route
    // value (e.g. "studentId") and projects it through IEffectiveScope before
    // letting the request through. Without scope binding the attribute keeps
    // its old action-only semantics.
    //
    // Policy name shape (encoded by HasPermissionAttribute, decoded by
    // PermissionPolicyProvider):
    //   Permission:{Module.Resource.Action}
    //   Permission:{Module.Resource.Action}|{ScopeKind}:{RouteValueName}
    //
    // Pipe is safe — PermissionIdentity never produces one.
    public HasPermissionAttribute(string permission)
    {
        Policy = $"{PermissionIdentity.Prefix}{PermissionIdentity.Parse(permission)}";
    }

    public HasPermissionAttribute(string permission, PermissionScopeKind scopeKind, string scopeRouteValue)
    {
        if (string.IsNullOrWhiteSpace(scopeRouteValue))
        {
            throw new ArgumentException("scopeRouteValue must be a non-empty route parameter name", nameof(scopeRouteValue));
        }

        if (scopeKind == PermissionScopeKind.None)
        {
            Policy = $"{PermissionIdentity.Prefix}{PermissionIdentity.Parse(permission)}";
            return;
        }

        Policy = $"{PermissionIdentity.Prefix}{PermissionIdentity.Parse(permission)}|{scopeKind}:{scopeRouteValue}";
    }
}
