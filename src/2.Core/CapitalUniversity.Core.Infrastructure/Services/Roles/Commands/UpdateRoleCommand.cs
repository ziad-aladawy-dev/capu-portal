using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Mappings;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;

public class UpdateRoleRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

public class UpdateRoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UpdateRoleCommandHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly ILocalizationService _localization;
    private readonly RoleMapper _mapper = new();

    public UpdateRoleCommandHandler(CoreDbContext dbContext, ILocalizationService localization)
    {
        _dbContext = dbContext;
        _localization = localization;
    }

    public async Task<UpdateRoleResponse?> Handle(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FindAsync(new object[] { request.Id }, cancellationToken);

        if (role == null) return null;

        if (request.Name != null) role.Name = LocalizedJson.Normalize(request.Name);
        _mapper.ApplyUpdate(request, role);
        role.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateRoleResponse
        {
            Id = role.Id,
            Name = _localization.Get<string>(role.Name)
        };

    }
}
