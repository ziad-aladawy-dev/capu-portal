namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Per-request snapshot of the caller's scope identity. Populated lazily on
/// first use from <see cref="ICurrentUser"/> + the Student/Staff row + active
/// StaffRoleAssignment grants. Cached for the request lifetime.
///
/// <para>
/// Scope is path-based: a Staff with a grant on <c>/Univ/Fac1</c> sees any row
/// whose <see cref="StructureNode.Path"/> starts with that prefix. A Student
/// sees only rows tied to its own node or an ancestor in its own path.
/// </para>
/// </summary>
public interface IUserScope
{
    /// <summary>Authenticated caller id (Staff.Id or Student.Id). Empty when unauthenticated.</summary>
    Guid UserId { get; }

    /// <summary>True if the caller is a Student row (vs Staff).</summary>
    bool IsStudent { get; }

    /// <summary>Caller's own structure node id. Null when unknown.</summary>
    Guid? OwnStructureNodeId { get; }

    /// <summary>Caller's own structure node path. Null when unknown.</summary>
    string? OwnStructureNodePath { get; }

    /// <summary>
    /// For staff: the set of granted path prefixes from active StaffRoleAssignments.
    /// Empty for students (they get path-based reverse matching against their own path).
    /// A "/" or null entry represents global scope and is normalised to "/".
    /// </summary>
    IReadOnlySet<string> AuthorizedNodePaths { get; }

    /// <summary>
    /// True when the caller has at least one global grant (null StructureNodeId on
    /// a StaffRoleAssignment). Short-circuits all path checks.
    /// </summary>
    bool HasGlobalScope { get; }

    /// <summary>Forces population. Idempotent; subsequent calls are no-ops.</summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
}
