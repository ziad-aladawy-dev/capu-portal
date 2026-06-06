namespace CapitalUniversity.Core.Abstractions.CrossCutting.Logging;

/// <summary>
/// Reserved metadata keys that the logger lifts off the metadata dictionary and
/// onto top-level <c>LogEntry</c> columns (so they are cheaply filterable on the
/// audit read API). A producer sets these on the metadata it passes to
/// <see cref="IAppLogger"/>; the logger moves them to the matching column and
/// removes them from the persisted metadata so they are not stored twice.
///
/// <para>
/// Using reserved keys keeps the <see cref="IAppLogger"/> signature unchanged
/// while still letting each producer (the EF audit trail, the auth audit logger,
/// the sync bridge) declare its category/action/entity explicitly.
/// </para>
/// </summary>
public static class AuditMetadataKeys
{
    /// <summary>Carries a <c>LogCategory</c> value → <c>LogEntry.Category</c>.</summary>
    public const string Category = "__auditCategory";

    /// <summary>Carries the friendly action verb (Created/Updated/Deleted) → <c>LogEntry.Action</c>.</summary>
    public const string Action = "__auditAction";

    /// <summary>Carries the audited entity type name → <c>LogEntry.EntityName</c>.</summary>
    public const string Entity = "__auditEntity";
}
