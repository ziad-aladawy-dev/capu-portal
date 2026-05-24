namespace CapitalUniversity.Core.Abstractions.Shared.BulkActions;

/// <summary>
/// Cross-cutting limits and conventions for Phase 1 bulk endpoints. Each
/// controller can override <see cref="MaxBulkSize"/> when it has a stricter
/// constraint, but should not exceed it.
/// </summary>
public static class BulkConstants
{
    /// <summary>
    /// Maximum number of ids accepted in a single bulk request. Anything larger
    /// should route through an async/Outbox-backed import job.
    /// </summary>
    public const int MaxBulkSize = 500;
}
