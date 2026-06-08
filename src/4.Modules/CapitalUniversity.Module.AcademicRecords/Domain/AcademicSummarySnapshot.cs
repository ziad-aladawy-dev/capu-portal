using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.AcademicRecords.Domain;

/// <summary>
/// A synchronized snapshot of a student's aggregate academic standing — GPA,
/// CGPA, credit totals, and standing label — exactly as an external academic
/// system reported them. Per <c>docs/Grades/Grades_Model.md</c> the portal does
/// NOT calculate any of these; it stores the delivered values and serves them
/// read-only.
///
/// <para>
/// One row per student is kept current by the sync layer (upserted by
/// <c>ExternalId</c> via the generic <c>ICoreWriteGateway</c>). The entity
/// implements <see cref="IExternallySourced"/> so the doc's <c>SyncedAt</c> maps
/// to <see cref="ExternallySourcedData.LastSyncedAt"/> with no grades-specific
/// sync code here.
/// </para>
/// </summary>
public class AcademicSummarySnapshot : BaseEntity, IExternallySourced
{
    /// <summary>
    /// Composed sync-provenance block carrying the upstream-merge-key +
    /// external-wins fields. Never null.
    /// </summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    /// <summary>The student this summary belongs to. FK-by-id; no nav.</summary>
    public Guid StudentId { get; set; }

    /// <summary>Synced grade-point average (current term / overall as defined upstream).</summary>
    public decimal Gpa { get; set; }

    /// <summary>Synced cumulative grade-point average.</summary>
    public decimal Cgpa { get; set; }

    public int EarnedCredits { get; set; }

    public int RemainingCredits { get; set; }

    public int PassedHours { get; set; }

    public int FailedHours { get; set; }

    /// <summary>Synced academic standing label (e.g. <c>"Good Standing"</c>, <c>"Probation"</c>).</summary>
    public string AcademicStanding { get; set; } = string.Empty;
}
