using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

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
    private readonly ILocalizationService _localization;

    public CreateRoleCommandHandler(CoreDbContext dbContext, ILocalizationService localization)
    {
        _dbContext = dbContext;
        _localization = localization;
    }

    public async Task<CreateRoleResponse> Handle(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = new Role
        {
            Name = LocalizedJson.Normalize(request.Name),
            IsSystemRole = false // Custom roles
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateRoleResponse
        {
            Id = role.Id,
            Name = _localization.Get<string>(role.Name)
        };
    }
}
