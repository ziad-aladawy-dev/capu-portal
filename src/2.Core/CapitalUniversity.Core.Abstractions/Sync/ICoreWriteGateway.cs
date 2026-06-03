using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.Sync;

/// <summary>
/// Single chokepoint for every write the sync layer performs against Core's
/// operational tables. Sync modules MUST go through this interface — they may
/// never resolve a <c>DbContext</c> or a Core repository directly. The gateway
/// owns: by-<c>ExternalId</c> merge, external-wins conflict resolution on
/// <c>ExternalUpdatedAt</c>, sync metadata stamping (<c>LastSyncedAt</c>,
/// <c>OriginSystem</c>), and the cross-entity Guid resolution helpers sync
/// needs to populate FKs.
/// <para>
/// The gateway is intentionally generic — Core.Abstractions cannot reference
/// module projects (Invoice lives in Module.Payments, ScheduleSlot in
/// Module.Schedule, CourseOffering in Module.CourseOffering), so any caller
/// that wants to upsert a module-owned entity supplies the type at the call
/// site. The compound constraint <c>where TEntity : BaseEntity, IExternallySourced</c>
/// gives the gateway the entity's <c>Id</c> (from BaseEntity) plus the composed
/// <see cref="ExternallySourcedData"/> block it merges on — without forcing
/// inheritance from a sync-aware base class.
/// </para>
/// </summary>
public interface ICoreWriteGateway
{
    /// <summary>
    /// Upsert a batch of entities that carry an <see cref="ExternallySourcedData"/>
    /// block. Lookup is by <c>entity.ExternallySourced.ExternalId</c>; when a
    /// match exists the supplied <paramref name="applyUpdate"/> delegate
    /// mutates the existing tracked instance (so the caller decides which
    /// scalar fields the sync layer actually owns — e.g. sync does NOT touch
    /// <c>Student.StructureNodeId</c> or <c>Staff.PasswordHash</c>). When no
    /// match exists and <see cref="CoreUpsertOptions.AllowInsert"/> is true,
    /// the incoming entity is added as-is. Sync metadata fields
    /// (<c>ExternallySourced.LastSyncedAt</c> and
    /// <c>ExternallySourced.OriginSystem</c>) are stamped by the gateway, not
    /// the caller.
    /// </summary>
    /// <param name="incoming">Sync's mapper output — fully-populated entities ready to merge.</param>
    /// <param name="applyUpdate">
    /// Called for each existing row that's about to be updated. Receives
    /// <c>(existing, incoming)</c> and mutates the <c>existing</c> instance
    /// in place. Sync uses this to copy only the columns it owns; everything
    /// else stays as Core last persisted it.
    /// </param>
    /// <param name="options">Insert / external-wins / per-call tunings.</param>
    Task<CoreUpsertResult> UpsertAsync<TEntity>(
        IReadOnlyList<TEntity> incoming,
        Action<TEntity, TEntity> applyUpdate,
        CoreUpsertOptions options,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity, IExternallySourced;

    /// <summary>
    /// Translate an upstream external key into the corresponding Core
    /// <see cref="BaseEntity.Id"/>. Used by sync modules that need to populate
    /// a Guid FK on the entity they're about to upsert (e.g. Invoice.StudentId
    /// from external student id, ScheduleSlot.CourseOfferingId from external
    /// offering id). Returns <c>null</c> when no Core row carries that external
    /// id yet.
    /// </summary>
    Task<Guid?> ResolveIdByExternalIdAsync<TEntity>(
        string externalId,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity, IExternallySourced;
}

/// <summary>Per-call tunings for <see cref="ICoreWriteGateway.UpsertAsync"/>.</summary>
public sealed class CoreUpsertOptions
{
    /// <summary>
    /// When <c>true</c>, rows whose <c>ExternalId</c> isn't in Core get inserted.
    /// When <c>false</c>, they're skipped + counted under
    /// <see cref="CoreUpsertResult.SkippedNotFound"/>.
    /// <para>
    /// Sync's Student / Staff writers pass <c>false</c> because the Core
    /// entities carry FKs (<c>StructureNodeId</c>) that aren't sourced from
    /// the sync pipeline — a sync-only insert would have no valid value for
    /// them. Inserts of those entities are an explicit Core admin flow.
    /// </para>
    /// </summary>
    public bool AllowInsert { get; init; } = true;

    /// <summary>
    /// External-wins guard: when both incoming and existing carry an
    /// <c>ExternallySourced.ExternalUpdatedAt</c> and the incoming is not
    /// strictly newer, the row is left untouched. Off-by-default would
    /// silently let stale upstream snapshots overwrite later state on retry.
    /// </summary>
    public bool RespectExternalUpdatedAt { get; init; } = true;
}

/// <summary>Per-batch outcome from <see cref="ICoreWriteGateway.UpsertAsync"/>.</summary>
public sealed class CoreUpsertResult
{
    /// <summary>Rows actually persisted (inserted + updated).</summary>
    public int Persisted { get; init; }

    /// <summary>
    /// Rows skipped because their ExternalId isn't in Core and
    /// <see cref="CoreUpsertOptions.AllowInsert"/> was <c>false</c>.
    /// </summary>
    public int SkippedNotFound { get; init; }

    /// <summary>
    /// Rows skipped because their incoming <c>ExternalUpdatedAt</c> wasn't
    /// newer than the existing row's. Indicates a benign replay / out-of-order
    /// upstream tick.
    /// </summary>
    public int SkippedNotNewer { get; init; }

    /// <summary>
    /// Rows skipped because they arrived with a blank <c>ExternalId</c> — the
    /// gateway can't merge without one.
    /// </summary>
    public int SkippedNoExternalId { get; init; }
}
