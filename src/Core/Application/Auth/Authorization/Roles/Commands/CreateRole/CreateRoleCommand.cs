using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Application.Auth.Authorization.Roles.Commands.CreateRole;

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CreateRoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Keeping it simple as a service/handler for now, if MediatR is used we can adapt.
public class CreateRoleCommandHandler
{
    private readonly CoreDbContext _dbContext;

    public CreateRoleCommandHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateRoleResponse> Handle(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = new Role
        {
            Name = request.Name,
            IsSystemRole = false // Custom roles
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateRoleResponse
        {
            Id = role.Id,
            Name = role.Name
        };
    }
}
