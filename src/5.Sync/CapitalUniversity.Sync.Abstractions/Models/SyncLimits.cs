namespace CapitalUniversity.Sync.Abstractions.Models;

/// <summary>
/// Single source of truth for sync-platform-wide numeric ceilings. Modules and the
/// pipeline both reference these constants so a future tightening (e.g. lowering
/// the SQL Server parameter-limit budget) lands in exactly one place.
///
/// <para>
/// Prior to this type the same <c>MaxBatchSize = 1000</c> ceiling was duplicated
/// in <c>SyncPipeline</c>, <c>StudentSyncOptionsValidator</c>, and
/// <c>StaffSyncOptionsValidator</c> — three definitions, one rule, drift-prone.
/// </para>
/// </summary>
public static class SyncLimits
{
    /// <summary>
    /// Hard upper bound on any <c>SyncPipelineRequest.BatchSize</c>. Chosen to stay
    /// below SQL Server's ~2100 parameter limit per command with a comfortable
    /// margin for additional per-query parameters that EF Core may emit alongside
    /// the writer's <c>WHERE ExternalId IN (@p0, @p1, …)</c> read.
    /// </summary>
    public const int MaxBatchSize = 1000;
}