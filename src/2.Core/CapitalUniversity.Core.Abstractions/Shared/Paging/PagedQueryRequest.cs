namespace CapitalUniversity.Core.Abstractions.Shared.Paging;

/// <summary>
/// Standard base for every list/search query DTO. Carries the four parameters
/// every paged endpoint needs: free-text <see cref="Search"/>, 1-indexed
/// <see cref="Page"/>, server-capped <see cref="PageSize"/>, and a single
/// <see cref="Sort"/> string.
/// </summary>
/// <remarks>
/// <para>
/// Sort encoding: comma-separated <c>field:asc|desc</c> pairs.
/// e.g. <c>sort=issuedAt:desc,amount:asc</c>. Direction defaults to <c>asc</c>
/// if omitted. Each endpoint exposes a whitelist of sort fields and rejects
/// anything outside it; see <see cref="SortClause.Parse"/>.
/// </para>
/// <para>
/// <see cref="PageSize"/> is clamped server-side at <see cref="PagingConstants.MaxPageSize"/>
/// — over-large requests are silently capped, not 400'd.
/// </para>
/// </remarks>
public abstract class PagedQueryRequest
{
    public string? Search { get; set; }

    /// <summary>1-indexed. Values &lt; 1 are treated as 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Default 20; capped at <see cref="PagingConstants.MaxPageSize"/>.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Comma-separated <c>field:asc|desc</c> pairs.</summary>
    public string? Sort { get; set; }

    /// <summary>Returns the 1-indexed page, never less than 1.</summary>
    public int NormalizedPage => Page < 1 ? 1 : Page;

    /// <summary>Returns the requested page size clamped to [1, MaxPageSize].</summary>
    public int NormalizedPageSize =>
        PageSize < 1 ? 1
        : PageSize > PagingConstants.MaxPageSize ? PagingConstants.MaxPageSize
        : PageSize;
}
