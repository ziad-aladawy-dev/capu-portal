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
/// One permission override for a single resource. <see cref="Level"/> remains
/// the legacy ladder shape exposed to clients — the server translates it to a
/// per-action grant set via the resource manifest's <c>Implies</c> graph on
/// write and back to the highest covered <see cref="ActionLevel"/> on read. New
/// callers may prefer <see cref="Actions"/>, which carries explicit action names
/// for resources whose grants don't map cleanly to the legacy CRUD ladder.
/// </summary>
public class PermissionOverrideModel
{
    public Guid ResourceId { get; set; }
    public ActionLevel Level { get; set; }
    public OverrideType Type { get; set; }

    /// <summary>
    /// Optional explicit per-action grant set. When populated on a write, this
    /// takes precedence over <see cref="Level"/>; when populated on a read it
    /// mirrors the underlying per-action rows.
    /// </summary>
    public List<string>? Actions { get; set; }
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
