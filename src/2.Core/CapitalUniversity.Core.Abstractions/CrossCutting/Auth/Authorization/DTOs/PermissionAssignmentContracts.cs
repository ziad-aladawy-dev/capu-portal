using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;

public class StructuralScopeModel
{
    public Guid? StructureNodeId { get; set; }
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
    public Guid? StructureNodeId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
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

/// <summary>
/// One permission override for a single resource, expressed as a per-action grant
/// set (<see cref="Actions"/>) in the canonical manifest model. On write the server
/// expands each action through the resource manifest's <c>Implies</c> graph —
/// forward closure for <see cref="OverrideType.Allow"/>, reverse closure for
/// <see cref="OverrideType.Deny"/> — and persists one row per action. On read it
/// mirrors the stored per-action rows.
/// <para>
/// Scope is optional and <b>per-override</b>: when <see cref="StructuralScope"/> and
/// <see cref="TemporalScope"/> are null the override inherits the request-level
/// scope (the same scope the role assignments use); when set, this override is
/// written at its own scope, independent of the role scope. This is what lets a
/// single assignment carry a role at one scope and an individual permission at a
/// different scope.
/// </para>
/// </summary>
public class PermissionOverrideModel
{
    public Guid ResourceId { get; set; }
    public OverrideType Type { get; set; }

    /// <summary>Canonical per-action grant set (action names). Required.</summary>
    public List<string> Actions { get; set; } = new();

    /// <summary>Optional per-override structural scope; inherits the request scope when null.</summary>
    public StructuralScopeModel? StructuralScope { get; set; }

    /// <summary>Optional per-override temporal scope; inherits the request scope when null.</summary>
    public TemporalScopeModel? TemporalScope { get; set; }
}

public class UpdatePermissionAssignmentRequest
{
    public Guid UserId { get; set; }

    public List<Guid> RolesToAdd { get; set; } = new();
    public List<Guid> RolesToRemove { get; set; } = new();

    public List<PermissionOverrideModel> PermissionsToAdd { get; set; } = new();

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
