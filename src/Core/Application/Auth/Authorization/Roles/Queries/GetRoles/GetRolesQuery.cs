using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Application.Auth.Authorization.Roles.Queries.GetRoleById;

namespace CapitalUniversity.Core.Application.Auth.Authorization.Roles.Queries.GetRoles;

public class GetRolesRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PagedRoleResponse
{
    public List<RoleResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class GetRolesQueryHandler
{
    private readonly CoreDbContext _dbContext;

    public GetRolesQueryHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedRoleResponse> Handle(GetRolesRequest request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Roles.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

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

        return new PagedRoleResponse
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
