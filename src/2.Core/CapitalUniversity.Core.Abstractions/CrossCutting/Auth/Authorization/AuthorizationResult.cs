namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

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
    public string? AppliedYear { get; }
    public string? AppliedSemester { get; }

    private AuthorizationResult(bool isAllowed, SourceType sourceType, Guid? sourceId, string? year, string? semester)
    {
        IsAllowed = isAllowed;
        SourceType = sourceType;
        SourceId = sourceId;
        AppliedYear = year;
        AppliedSemester = semester;
    }

    public static AuthorizationResult Deny() => new AuthorizationResult(false, SourceType.None, null, null, null);

    public static AuthorizationResult AllowFromOverride(Guid overrideId, string? year, string? semester)
        => new AuthorizationResult(true, SourceType.UserOverride, overrideId, year, semester);

    public static AuthorizationResult AllowFromRole(Guid roleId, string? year, string? semester)
        => new AuthorizationResult(true, SourceType.RoleAssignment, roleId, year, semester);
}
