namespace CapitalUniversity.Core.Domain.Common;

/// <summary>
/// Sync provenance + upstream-merge-key fields for entities that may be sourced
/// from an upstream system. This is a <b>composed data model</b>, not a base
/// class: entities still inherit from <see cref="BaseEntity"/> directly and
/// expose this block as a property (see <see cref="IExternallySourced"/>).
///
/// <para>
/// Every field is nullable except <see cref="OriginSystem"/>: not every row
/// originated upstream. A <c>Student</c> created through Core's normal admin
/// flow has no upstream identity and carries <see cref="ExternalId"/> = <c>null</c>;
/// the same Student row, once an upstream tick touches it, gets the upstream's
/// stable id stamped in. The recommended SQL Server index on
/// <see cref="ExternalId"/> is a <i>filtered</i> unique index
/// (<c>WHERE ExternalId IS NOT NULL</c>) so multiple Core-only rows can coexist
/// while sync-sourced rows are still guaranteed unique.
/// </para>
///
/// <para>
/// <see cref="OriginSystem"/> defaults to <c>"internal"</c> — Core-created
/// rows look like local creation by default. The sync write gateway flips it
/// to <c>"external"</c> when an upstream tick is the source.
/// </para>
/// </summary>
public sealed class ExternallySourcedData
{
    /// <summary>
    /// Upstream system's stable identifier for this entity. <c>null</c> for
    /// Core-created rows that were never sourced from sync. The sync layer
    /// uses this as the upsert merge key.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Upstream's last-modified stamp at the point we synced (UTC). Used for
    /// external-wins conflict resolution: a stale upstream tick whose
    /// <c>ExternalUpdatedAt</c> is older than the row's current value is
    /// silently no-op'd by the gateway.
    /// </summary>
    public DateTime? ExternalUpdatedAt { get; set; }

    /// <summary>
    /// Upstream's monotonic version (if exposed). Optional secondary signal
    /// for conflict resolution when timestamps tie.
    /// </summary>
    public int? ExternalVersion { get; set; }

    /// <summary>
    /// When the sync pipeline last touched this row (UTC). <c>null</c> for
    /// rows never touched by sync.
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Provenance tag. <c>"internal"</c> (default) for Core-created rows;
    /// <c>"external"</c> for sync-sourced rows. Kept as a string rather than
    /// an enum so additional origin tags (e.g. <c>"migrated"</c> from a one-off
    /// data import) can be introduced without a domain enum change.
    /// </summary>
    public string OriginSystem { get; set; } = "internal";
}
