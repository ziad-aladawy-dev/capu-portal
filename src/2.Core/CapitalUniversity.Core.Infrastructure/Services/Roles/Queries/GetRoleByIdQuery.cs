using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Application.Auth.Authorization.Roles.Queries.GetRoleById;

public class GetRoleByIdRequest
{
    public Guid Id { get; set; }
}

public class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
}

public class GetRoleByIdQueryHandler
{
    private readonly CoreDbContext _dbContext;

    public GetRoleByIdQueryHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoleResponse?> Handle(GetRoleByIdRequest request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FindAsync(new object[] { request.Id }, cancellationToken);

        if (role == null) return null;

        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            IsSystemRole = role.IsSystemRole
        };
    }
}
