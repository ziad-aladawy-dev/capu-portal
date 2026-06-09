using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Registration.Abstractions;

namespace CapitalUniversity.Modules.Registration.Domain;

/// <summary>
/// The authoritative Student ↔ Course relationship inside the portal: one
/// course-registration attempt as delivered by an external academic system
/// through the Sync Platform. Per <c>docs/Courses/Registration_Model.md</c> this
/// is a read-model snapshot — the portal never registers, drops, or modifies
/// enrollments, so the entity carries no workflow / lifecycle behaviour, only
/// the synced fields plus sync provenance.
///
/// <para>
/// A student may repeat a course, so each attempt is stored independently and
/// ordered by <see cref="AttemptNumber"/> (e.g. Calculus I in Fall 2022 and
/// again in Fall 2023 are two rows). The term is the project's existing
/// <c>Semester</c> entity (referenced by <see cref="SemesterId"/>) — semester
/// definitions are not duplicated here; ordering/academic-year metadata is read
/// back from <c>Semester</c>.
/// </para>
///
/// <para>
/// All cross-module references are by id with no EF navigation, matching the
/// modularity rule used by <c>CourseOffering</c> / <c>Invoice</c> /
/// <c>StudentProfileRecord</c>. The entity implements
/// <see cref="IExternallySourced"/> so the generic <c>ICoreWriteGateway</c> can
/// upsert it by <c>ExternalId</c> with no registration-specific sync code in
/// this module — the doc's <c>ExternalReferenceId</c> maps to
/// <see cref="ExternallySourcedData.ExternalId"/> and <c>SyncedAt</c> to
/// <see cref="ExternallySourcedData.LastSyncedAt"/>.
/// </para>
/// </summary>
public class StudentRegisteredCourse : BaseEntity, IExternallySourced
{
    /// <summary>
    /// Composed sync-provenance block. <see cref="BaseEntity"/> inheritance is
    /// untouched; this property carries the upstream-merge-key + external-wins
    /// fields the sync write gateway needs.
    /// </summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    /// <summary>Registered student. FK-by-id; no nav.</summary>
    public Guid StudentId { get; set; }

    /// <summary>Catalog course the student registered in. FK-by-id; no nav.</summary>
    public Guid CourseId { get; set; }

    /// <summary>Academic term (<c>Semester</c>) the attempt belongs to. FK-by-id; no nav.</summary>
    public Guid SemesterId { get; set; }

    /// <summary>Organizational target (program/department) the registration was opened under. FK-by-id; no nav.</summary>
    public Guid StructureNodeId { get; set; }

    /// <summary>1-based attempt ordinal, preserving repeat order for the same (student, course).</summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>Upstream-reported lifecycle of the attempt. The portal stores it verbatim.</summary>
    public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Enrolled;

    /// <summary>When the registration was made upstream (UTC).</summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>When the attempt reached a terminal status upstream (UTC); null while still enrolled.</summary>
    public DateTime? CompletedAt { get; set; }
}
