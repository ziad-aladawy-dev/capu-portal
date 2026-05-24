namespace CapitalUniversity.Core.Abstractions.Shared.Paging;

/// <summary>
/// Cross-cutting limits for paged endpoints. Centralized so Phase-2 list
/// endpoints don't drift to per-resource caps.
/// </summary>
public static class PagingConstants
{
    /// <summary>Maximum page size accepted by any paged endpoint. Larger values are silently clamped.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Default page size when callers don't supply one.</summary>
    public const int DefaultPageSize = 20;
}
