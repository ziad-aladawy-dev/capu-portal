using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization.DTOs
;

public class StructuralScopeModel
{
    public Guid? FacultyId { get; set; }
    public bool AllFaculties { get; set; }
    public Guid? ProgramId { get; set; }
    public bool AllPrograms { get; set; }
}

public class TemporalScopeModel
{
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
    public bool AlwaysActive { get; set; }
}

public class GetPermissionAssignmentQueryDto
{
    public Guid UserId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
    public bool AllFaculties { get; set; }
    public bool AllPrograms { get; set; }
    public bool AlwaysActive { get; set; }
}

public class CreatePermissionAssignmentRequest
{
    public Guid UserId { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
    public List<PermissionOverrideModel> PermissionOverrides { get; set; } = new();
    public StructuralScopeModel StructuralScope { get; set; } = new();
    public TemporalScopeModel TemporalScope { get; set; } = new();
}

public class PermissionOverrideModel
{
    public Guid ServiceId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public ActionLevel Level { get; set; }
    public OverrideType Type { get; set; }
}

public class UpdatePermissionAssignmentRequest
{
    public Guid UserId { get; set; }

    public List<Guid> RolesToAdd { get; set; } = new();
    public List<Guid> RolesToRemove { get; set; } = new();

    public List<PermissionOverrideModel> PermissionsToAdd { get; set; } = new();

    // For overrides to remove, we could pass the ServiceId and Resource to identify them
    public List<PermissionOverrideModel> PermissionsToRemove { get; set; } = new();

    public StructuralScopeModel StructuralScope { get; set; } = new();
    public TemporalScopeModel TemporalScope { get; set; } = new();
}

public class PermissionAssignmentResponse
{
    public Guid UserId { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
    public List<PermissionOverrideModel> PermissionOverrides { get; set; } = new();
    public StructuralScopeModel StructuralScope { get; set; } = new();
    public TemporalScopeModel TemporalScope { get; set; } = new();
}
