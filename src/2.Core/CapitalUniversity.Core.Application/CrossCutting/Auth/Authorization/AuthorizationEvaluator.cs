using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization;

public class AuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly ILoggerService _logger;

    public AuthorizationEvaluator(ILoggerService logger)
    {
        _logger = logger;
    }

    public AuthorizationResult Evaluate(
        Guid userId,
        string resource,
        ActionLevel requiredLevel,
        bool isClosed,
        AuthorizationScope scope,
        IEnumerable<IUserPermissionOverride> overrides,
        IEnumerable<IUserRoleAssignment> assignments,
        IEnumerable<IRolePermission> rolePermissions)
    {
        var relevantOverrides = overrides
            .Where(o => (o.Resource == resource || o.Resource == "*") && 
                        !string.IsNullOrEmpty(scope.StructureNodePath) &&
                        !string.IsNullOrEmpty(o.StructureNodePath) &&
                        (scope.StructureNodePath + "/").StartsWith(o.StructureNodePath + "/") &&
                        o.Year == scope.Year && 
                        o.Semester == scope.Semester)
            .ToList();

        // 1. Evaluate UserPermissionOverrides (DENY)
        var denyOverride = relevantOverrides.FirstOrDefault(o => o.Type == OverrideType.Deny && o.Level <= requiredLevel);
        if (denyOverride != null)
        {
            LogDecision(userId, "EvaluatePermission", resource, false, SourceType.UserOverride, denyOverride.Id, scope);
            return AuthorizationResult.Deny();
        }

        // 2. Compute highest ActionLevel from ALLOW Overrides and Roles (RBAC)
        ActionLevel maxGrantedLevel = ActionLevel.None;
        SourceType decisionSourceType = SourceType.None;
        Guid? decisionSourceId = null;

        var allowOverride = relevantOverrides
            .Where(o => o.Type == OverrideType.Allow)
            .OrderByDescending(o => o.Level)
            .FirstOrDefault();

        if (allowOverride != null)
        {
            maxGrantedLevel = allowOverride.Level;
            decisionSourceType = SourceType.UserOverride;
            decisionSourceId = allowOverride.Id;
        }


        var matchingAssignments = assignments
            .Where(a => !string.IsNullOrEmpty(scope.StructureNodePath) &&
                        !string.IsNullOrEmpty(a.StructureNodePath) &&
                        (scope.StructureNodePath + "/").StartsWith(a.StructureNodePath + "/") &&
                        a.Year == scope.Year && 
                        a.Semester == scope.Semester)
            .ToList();

        foreach (var assignment in matchingAssignments)
        {
            var maxForRole = rolePermissions
                .Where(rp => rp.RoleId == assignment.RoleId && (rp.Resource == resource || rp.Resource == "*"))
                .Select(rp => rp.Level)
                .DefaultIfEmpty(ActionLevel.None)
                .Max();

            if (maxForRole > maxGrantedLevel)
            {
                maxGrantedLevel = maxForRole;
                decisionSourceType = SourceType.RoleAssignment;
                decisionSourceId = assignment.RoleId;
            }
        }

        // 3. Apply ABAC constraints
        bool isAllowed = false;
        if (isClosed)
        {
            if (requiredLevel == ActionLevel.Delete)
            {
                isAllowed = maxGrantedLevel >= ActionLevel.Delete;
            }
            else if (requiredLevel == ActionLevel.Insert || requiredLevel == ActionLevel.EditClose)
            {
                // Closed records require at least Open level (Level 4) to modify
                isAllowed = maxGrantedLevel >= ActionLevel.Open;
            }
            else
            {
                isAllowed = maxGrantedLevel >= requiredLevel;
            }
        }
        else
        {
            isAllowed = maxGrantedLevel >= requiredLevel;
        }

        if (isAllowed)
        {
            var result = decisionSourceType == SourceType.UserOverride 
                ? AuthorizationResult.AllowFromOverride(decisionSourceId.GetValueOrDefault(), scope.Domain, scope.Year, scope.Semester)
                : AuthorizationResult.AllowFromRole(decisionSourceId.GetValueOrDefault(), scope.Domain, scope.Year, scope.Semester);
                
            LogDecision(userId, "EvaluatePermission", resource, true, decisionSourceType, decisionSourceId, scope);
            return result;
        }

        LogDecision(userId, "EvaluatePermission", resource, false, SourceType.None, null, scope);
        return AuthorizationResult.Deny();
    }

    private void LogDecision(Guid userId, string operation, string resource, bool isAllowed, SourceType sourceType, Guid? sourceId, AuthorizationScope scope)
    {
        var resultText = isAllowed ? "ALLOW" : "DENY";
        var scopeInfo = $"Node:{scope.StructureNodeId}, Year:{scope.Year}, Sem:{scope.Semester}";
        var message = $"AuthZ Decision: {resultText} | Source: {sourceType} ({sourceId}) | Scope: {scopeInfo}";

        _logger.LogInformation(message, resource);
    }
}
