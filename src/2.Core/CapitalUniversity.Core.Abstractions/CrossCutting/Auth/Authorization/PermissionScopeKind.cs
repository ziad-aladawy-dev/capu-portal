namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Tells the permission handler which <see cref="IEffectiveScope"/> dimension to
/// project a route value through when the controller decorates an endpoint with
/// <c>[HasPermission(..., scopeRouteValue: "studentId", scopeKind: PermissionScopeKind.Student)]</c>.
/// <c>None</c> means action-only enforcement (legacy behaviour).
/// </summary>
public enum PermissionScopeKind
{
    None = 0,
    Student = 1,
    StructureNode = 2,
    AcademicYear = 3,
    Semester = 4,
}
