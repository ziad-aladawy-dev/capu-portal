using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;

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
    private readonly ILocalizationService _localization;

    public GetRoleByIdQueryHandler(CoreDbContext dbContext, ILocalizationService localization)
    {
        _dbContext = dbContext;
        _localization = localization;
    }

    public async Task<RoleResponse?> Handle(GetRoleByIdRequest request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FindAsync(new object[] { request.Id }, cancellationToken);

        if (role == null) return null;

        return new RoleResponse
        {
            Id = role.Id,
            Name = _localization.Get<string>(role.Name),
            IsSystemRole = role.IsSystemRole
        };
    }
}
