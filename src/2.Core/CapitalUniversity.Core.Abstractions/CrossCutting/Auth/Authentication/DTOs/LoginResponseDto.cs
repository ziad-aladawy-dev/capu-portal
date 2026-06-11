using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

public class LoginResponseDto
{
    public UserInfoDto User { get; set; } = new();
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Raw refresh token. Long-lived (default 30 days), single-use: every call to
    /// /auth/refresh rotates this and invalidates the previous one. Persist only in
    /// secure client storage — the server stores SHA-256 of this value, never the
    /// raw string.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    public List<PermissionDto> Permissions { get; set; } = new();
    public ActiveScopeDto ActiveScope { get; set; } = new();

    /// <summary>
    /// When the user's password expires (UTC). Null when no expiry applies
    /// (e.g. staff accounts). The client uses this to warn ahead of expiry.
    /// </summary>
    public DateTime? PasswordExpiryDate { get; set; }

    /// <summary>
    /// True when the password is already expired and the client must force a
    /// password change before granting access to the rest of the app. The access
    /// token is still issued so the change-password call can be authorized.
    /// </summary>
    public bool RequiresPasswordChange { get; set; }
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The caller's role (e.g. "Student", "Super Admin"). Needed by the SPA to
    /// pick the correct shell and gate routes — in particular, a Student is
    /// context-scoped and holds no permission entries, so the client recognises
    /// the role to grant access to the student surface. Not PII.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public UserAttributesDto Attributes { get; set; } = new();
}

public class UserAttributesDto
{
    public string Uni { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class ActiveScopeDto
{
    public StructuralScopeDto Structural { get; set; } = new();
    public TemporalScopeDto Temporal { get; set; } = new();
}

public class StructuralScopeDto
{
    public Guid? NodeId { get; set; }
}

public class TemporalScopeDto
{
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
}
