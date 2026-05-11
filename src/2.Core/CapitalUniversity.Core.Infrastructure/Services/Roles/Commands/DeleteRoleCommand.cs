using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Application.Auth.Authorization.Roles.Commands.DeleteRole;

public class DeleteRoleRequest
{
    public Guid Id { get; set; }
}

public class DeleteRoleCommandHandler
{
    private readonly CoreDbContext _dbContext;

    public DeleteRoleCommandHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FindAsync(new object[] { request.Id }, cancellationToken);

        if (role == null) return false;

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
