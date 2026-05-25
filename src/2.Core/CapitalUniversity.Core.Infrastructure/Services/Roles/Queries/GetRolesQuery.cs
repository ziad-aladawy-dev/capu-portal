using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;

public class GetRolesRequest : PagedQueryRequest
{
    /// <summary>Filter by system-role flag. <c>null</c> returns both.</summary>
    public bool? IsSystem { get; set; }
}

public class PagedRoleResponse
{
    public List<RoleResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class GetRolesQueryHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly ILocalizationService _localization;

    public GetRolesQueryHandler(CoreDbContext dbContext, ILocalizationService localization)
    {
        _dbContext = dbContext;
        _localization = localization;
    }

    public async Task<PagedRoleResponse> Handle(GetRolesRequest request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            // Raw JSON-encoded Name — Contains works against bilingual JSON.
            query = query.Where(r => r.Name.Contains(s));
        }
        if (request.IsSystem.HasValue)
        {
            query = query.Where(r => r.IsSystemRole == request.IsSystem.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Server-side sort by the raw column. Two callers under different
        // cultures see the same physical order — sorting by the decoded
        // (per-culture) Name would change collation per request and is more
        // expensive than ordering by id once the catalog grows.
        var items = await query
            .OrderBy(r => r.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                IsSystemRole = r.IsSystemRole
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.Name = _localization.Get<string>(item.Name);
        }

        return new PagedRoleResponse
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
