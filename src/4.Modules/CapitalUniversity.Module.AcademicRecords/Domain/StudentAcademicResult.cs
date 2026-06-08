using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;

namespace CapitalUniversity.Modules.AcademicRecords.Domain;

/// <summary>
/// The synchronized outcome of one course-registration attempt: the grade,
/// score, status, and earned credits an external academic system recorded for a
/// <c>StudentRegisteredCourse</c>. Per <c>docs/Grades/Grades_Model.md</c> this is
/// a read-model snapshot — the portal never enters, modifies, or calculates
/// grades, so the entity carries no workflow / lifecycle behaviour, only the
/// synced fields plus sync provenance.
///
/// <para>
/// This module does NOT own the Student ↔ Course enrollment relationship: it
/// references the registration attempt by id (<see cref="StudentRegisteredCourseId"/>)
/// and reads student / course / term back through it, matching the modularity
/// rule used across the codebase (cross-module references are by id with no EF
/// navigation).
/// </para>
///
/// <para>
/// The entity implements <see cref="IExternallySourced"/> so the generic
/// <c>ICoreWriteGateway</c> can upsert it by <c>ExternalId</c> with no
/// grades-specific sync code in this module — the doc's <c>ExternalReferenceId</c>
/// maps to <see cref="ExternallySourcedData.ExternalId"/> and <c>SyncedAt</c> to
/// <see cref="ExternallySourcedData.LastSyncedAt"/>.
/// </para>
/// </summary>
public class StudentAcademicResult : BaseEntity, IExternallySourced
{
    /// <summary>
    /// Composed sync-provenance block. <see cref="BaseEntity"/> inheritance is
    /// untouched; this property carries the upstream-merge-key + external-wins
    /// fields the sync write gateway needs.
    /// </summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    /// <summary>The registration attempt (<c>StudentRegisteredCourse</c>) this result grades. FK-by-id; no nav.</summary>
    public Guid StudentRegisteredCourseId { get; set; }

    /// <summary>Letter grade as synced (e.g. <c>"A"</c>, <c>"B+"</c>). Null for non-completed attempts.</summary>
    public string? Grade { get; set; }

    /// <summary>Numeric score as synced. Null for non-completed attempts.</summary>
    public decimal? NumericScore { get; set; }

    /// <summary>Upstream-reported outcome of the attempt. The portal stores it verbatim.</summary>
    public AcademicResultStatus Status { get; set; } = AcademicResultStatus.InProgress;

    /// <summary>Credits awarded by this attempt (synced; 0 for non-passing outcomes).</summary>
    public int CreditsEarned { get; set; }

    /// <summary>
    /// True when this is the most recent attempt of the course for the student.
    /// Synced from upstream; transcript output uses only latest-attempt rows.
    /// </summary>
    public bool IsLatestAttempt { get; set; }
}
