using CapitalUniversity.Modules.Registration.Abstractions;

namespace CapitalUniversity.Sync.Registration.Domain;

/// <summary>
/// Upstream shape of one course-registration attempt from the external academic
/// system. Maps to Core's <c>Modules.Registration.Domain.StudentRegisteredCourse</c>.
///
/// <para>
/// Cross-entity references split by ownership, mirroring how
/// <c>Sync.Schedules</c> resolves <c>CourseOffering</c>:
/// <list type="bullet">
///   <item><see cref="ExternalStudentId"/> / <see cref="ExternalCourseId"/> are
///   upstream keys for entities that themselves flow through sync (Student,
///   Course are <c>IExternallySourced</c>); the writer resolves them to Core
///   ids via <c>ICoreWriteGateway.ResolveIdByExternalIdAsync</c>.</item>
///   <item><see cref="SemesterId"/> / <see cref="StructureNodeId"/> are Core
///   ids carried verbatim. Terms and org-units are portal-native reference
///   data (admin/seeded, not <c>IExternallySourced</c>), so the upstream is
///   provisioned with the portal's stable identifiers at integration setup
///   rather than inventing external keys the gateway could not resolve.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ExternalRegistration
{
    /// <summary>Upstream stable id for this registration — the sync merge key.</summary>
    public required string ExternalRegistrationId { get; init; }

    /// <summary>Upstream student key; resolved to Core <c>StudentId</c> by the writer.</summary>
    public required string ExternalStudentId { get; init; }

    /// <summary>Upstream course key; resolved to Core <c>CourseId</c> by the writer.</summary>
    public required string ExternalCourseId { get; init; }

    /// <summary>Core term id (portal-native reference data) the attempt belongs to.</summary>
    public required Guid SemesterId { get; init; }

    /// <summary>Core structure-node id (portal-native reference data) the registration was opened under.</summary>
    public required Guid StructureNodeId { get; init; }

    /// <summary>1-based attempt ordinal, preserving repeat order for the same (student, course).</summary>
    public required int AttemptNumber { get; init; }

    /// <summary>Upstream-reported lifecycle of the attempt.</summary>
    public required RegistrationStatus Status { get; init; }

    /// <summary>When the registration was made upstream.</summary>
    public required DateTimeOffset RegisteredAt { get; init; }

    /// <summary>When the attempt reached a terminal status upstream; null while still enrolled.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Upstream last-modified stamp — drives the extractor cursor and external-wins guard.</summary>
    public required DateTimeOffset ExternalUpdatedAt { get; init; }

    /// <summary>Upstream monotonic version — secondary conflict-resolution signal.</summary>
    public required int ExternalVersion { get; init; }
}
