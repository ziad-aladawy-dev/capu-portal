using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;

public class LoginResponseDto
{
    public UserInfoDto User { get; set; }
    public string Token { get; set; }
    public AuthorizedScopesDto AuthorizedScopes { get; set; }
    public List<PermissionDto> Permissions { get; set; }
    public ActiveScopeDto ActiveScope { get; set; }
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public UserAttributesDto Attributes { get; set; }
}

public class UserAttributesDto
{
    public string Uni { get; set; }
    public string Faculty { get; set; }
    public string Department { get; set; }
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
    public string Key { get; set; }
    public string Module { get; set; }
    public string Resource { get; set; }
    public string Action { get; set; }
}

public class ActiveScopeDto
{
    public StructuralScopeDto Structural { get; set; }
    public TemporalScopeDto Temporal { get; set; }
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
