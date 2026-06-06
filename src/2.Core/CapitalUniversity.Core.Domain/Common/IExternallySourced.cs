namespace CapitalUniversity.Core.Domain.Common;

/// <summary>
/// Marker contract for entities that may be sourced from an upstream system
/// through the sync layer. Implementing entities still inherit from
/// <see cref="BaseEntity"/> directly — this is composition (a property block),
/// not class inheritance.
///
/// <para>
/// The sync write gateway constrains <c>TEntity</c> on this interface so it
/// can read and stamp the <see cref="ExternallySourced"/> block uniformly
/// across every sync-eligible type (Student, Staff, Course, Invoice,
/// ScheduleSlot, CourseOffering) without itself referencing module-owned
/// types or duplicating per-entity merge logic.
/// </para>
/// </summary>
public interface IExternallySourced
{
    /// <summary>
    /// The composed sync-provenance + upstream-merge-key block. The instance
    /// is owned by the implementing entity (never <c>null</c>): the gateway
    /// reads <c>ExternalId</c> as the merge key and writes back
    /// <c>LastSyncedAt</c> / <c>OriginSystem</c> / <c>ExternalUpdatedAt</c>
    /// / <c>ExternalVersion</c> after the per-row merge delegate runs.
    /// </summary>
    ExternallySourcedData ExternallySourced { get; set; }
}
