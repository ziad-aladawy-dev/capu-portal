namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Row-level access decision for the three in-scope services (Invoice,
/// AcademicPlan, StudentProfileRecord). Services call this BEFORE returning
/// any payload — cached or fresh — so the shared object cache cannot leak
/// across users.
///
/// <para>
/// Out-of-scope is reported by returning <c>false</c>; the caller maps that
/// to a 404 (existence-leak avoidance per RemediationPlan.md P1.1).
/// </para>
/// </summary>
public interface IEffectiveScope
{
    /// <summary>
    /// True when the caller may see rows owned by <paramref name="studentId"/>.
    /// Student callers: only when <paramref name="studentId"/> is their own id.
    /// Staff callers: when one of their authorized path prefixes covers the
    /// student's StructureNode path.
    /// </summary>
    Task<bool> CanAccessStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the caller may see rows owned by <paramref name="structureNodeId"/>.
    /// Staff: standard path-prefix grant match. Student: the node must be on
    /// the student's own ancestor path (so they see their own program/plan).
    /// </summary>
    Task<bool> CanAccessStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the caller's active temporal scope (from headers) covers the
    /// requested <paramref name="academicYearId"/>.
    /// </summary>
    Task<bool> CanAccessAcademicYearAsync(Guid academicYearId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the caller's active temporal scope (from headers) covers the
    /// requested <paramref name="semesterId"/>.
    /// </summary>
    Task<bool> CanAccessSemesterAsync(Guid semesterId, CancellationToken cancellationToken = default);
}
