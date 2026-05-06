using System;

namespace CapitalUniversity.Core.Abstractions.Auth.Authorization;

public enum SourceType
{
    None = 0,
    RoleAssignment = 1,
    UserOverride = 2
}

public class AuthorizationResult
{
    public bool IsAllowed { get; }
    public SourceType SourceType { get; }
    public Guid? SourceId { get; }
    public string AppliedDomain { get; }
    public string AppliedYear { get; }
    public string AppliedSemester { get; }

    private AuthorizationResult(bool isAllowed, SourceType sourceType, Guid? sourceId, string domain, string year, string semester)
    {
        IsAllowed = isAllowed;
        SourceType = sourceType;
        SourceId = sourceId;
        AppliedDomain = domain;
        AppliedYear = year;
        AppliedSemester = semester;
    }

    public static AuthorizationResult Deny() => new AuthorizationResult(false, SourceType.None, null, null, null, null);
    
    public static AuthorizationResult AllowFromOverride(Guid overrideId, string domain, string year, string semester) 
        => new AuthorizationResult(true, SourceType.UserOverride, overrideId, domain, year, semester);
        
    public static AuthorizationResult AllowFromRole(Guid roleId, string domain, string year, string semester) 
        => new AuthorizationResult(true, SourceType.RoleAssignment, roleId, domain, year, semester);
}
