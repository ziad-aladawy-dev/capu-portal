using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.API.Infrastructure;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionManagementService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly IEffectiveScope _scope;
    private readonly IUserScope _userScope;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthAuditLogger? _audit;

    public PermissionHandler(
        IPermissionManagementService permissionService,
        ICurrentUser currentUser,
        IEffectiveScope scope,
        IUserScope userScope,
        IHttpContextAccessor httpContextAccessor,
        IAuthAuditLogger? audit = null)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
        _scope = scope;
        _userScope = userScope;
        _httpContextAccessor = httpContextAccessor;
        _audit = audit;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_currentUser.Id == Guid.Empty)
        {
            return;
        }

        // Optimized hashtable lookup (HashSet)
        var permissions = await _permissionService.GetPermissionLookupAsync(_currentUser.Id);

        // Students hold no role grants (context-scoped model), so a fixed set of
        // self-service permissions is granted IMPLICITLY to IsStudent callers.
        // Self-access is still enforced below by the scope binding (for
        // Student-scoped endpoints) or by the controller binding studentId from
        // the JWT. Without this, every student request 403'd — the "unauthorized
        // login loop". The grant is narrow: only StudentSelfPermissions entries,
        // never an ops/admin resource.
        var hasPermission = permissions.Contains(requirement.Permission)
            || (_userScope.IsStudent && StudentSelfPermissions.Contains(requirement.Permission));

        if (!hasPermission)
        {
            if (_audit is not null)
            {
                await _audit.LogPermissionDeniedAsync(_currentUser.Id, requirement.Permission);
            }
            return;
        }

        // C3 — When the policy carries a scope binding, project the route value
        // through IEffectiveScope. The action grant is necessary but not enough:
        // the caller must also be in-scope for the specific resource id. A
        // mismatch here is logged at the same severity as a missing permission
        // because it's an authorization decision, not a routing issue.
        if (requirement.ScopeKind != PermissionScopeKind.None && !string.IsNullOrWhiteSpace(requirement.ScopeRouteValue))
        {
            if (!await EvaluateScopeAsync(requirement))
            {
                if (_audit is not null)
                {
                    await _audit.LogPermissionDeniedAsync(_currentUser.Id, $"{requirement.Permission}|scope:{requirement.ScopeKind}");
                }
                return;
            }
        }

        context.Succeed(requirement);
    }

    private async Task<bool> EvaluateScopeAsync(PermissionRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            // No HTTP context (background work, tests in raw handler mode).
            // Without a request we cannot evaluate scope; refuse rather than
            // succeed silently.
            return false;
        }

        if (!TryGetRouteGuid(httpContext, requirement.ScopeRouteValue!, out var resourceId))
        {
            return false;
        }

        return requirement.ScopeKind switch
        {
            PermissionScopeKind.Student       => await _scope.CanAccessStudentAsync(resourceId, httpContext.RequestAborted),
            PermissionScopeKind.StructureNode => await _scope.CanAccessStructureNodeAsync(resourceId, httpContext.RequestAborted),
            PermissionScopeKind.AcademicYear  => await _scope.CanAccessAcademicYearAsync(resourceId, httpContext.RequestAborted),
            PermissionScopeKind.Semester      => await _scope.CanAccessSemesterAsync(resourceId, httpContext.RequestAborted),
            _ => false,
        };
    }

    private static bool TryGetRouteGuid(HttpContext context, string routeName, out Guid value)
    {
        value = Guid.Empty;

        // Prefer route data; fall back to query string so endpoints with the id
        // in either place still get scope-guarded.
        if (context.Request.RouteValues.TryGetValue(routeName, out var routeValue)
            && routeValue is not null
            && Guid.TryParse(routeValue.ToString(), out var parsedFromRoute))
        {
            value = parsedFromRoute;
            return true;
        }

        if (context.Request.Query.TryGetValue(routeName, out var queryValue)
            && Guid.TryParse(queryValue.ToString(), out var parsedFromQuery))
        {
            value = parsedFromQuery;
            return true;
        }

        return false;
    }
}
