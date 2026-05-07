using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;

public class LoginResponseDto
{
    public UserInfoDto User { get; set; } = new();
    public string Token { get; set; } = string.Empty;
    public AuthorizedScopesDto AuthorizedScopes { get; set; } = new();
    public List<PermissionDto> Permissions { get; set; } = new();
    public ActiveScopeDto ActiveScope { get; set; } = new();
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserAttributesDto Attributes { get; set; } = new();
}

public class UserAttributesDto
{
    public string Uni { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class AuthorizedScopesDto
{
    public List<Guid> AllowedFacultyIds { get; set; } = new();
    public List<Guid> AllowedProgramIds { get; set; } = new();
    public List<Guid> AllowedAcademicYearIds { get; set; } = new();
    public List<Guid> AllowedSemesterIds { get; set; } = new();
}

public class PermissionDto
{
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public class ActiveScopeDto
{
    public StructuralScopeDto Structural { get; set; } = new();
    public TemporalScopeDto Temporal { get; set; } = new();
}

public class StructuralScopeDto
{
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }
}

public class TemporalScopeDto
{
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
}
