using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;

public class PermissionTreeQueryHandler : IPermissionTreeQueryHandler
{
    private readonly CoreDbContext _dbContext;

    // Canonical action expansion. Names + descriptions are presentation concerns; the
    // PermissionName itself is built from Action via PermissionIdentity.Create so it
    // round-trips against seeded role permissions and policy checks.
    private static readonly (ActionLevel Level, string Action, string Description)[] PermissionActions =
    {
        (ActionLevel.View,      "View",      "Can view records"),
        (ActionLevel.Insert,    "Insert",    "Can add new records"),
        (ActionLevel.EditClose, "EditClose", "Can edit existing records"),
        (ActionLevel.Open,      "Open",      "Can open/unlock records"),
        (ActionLevel.Delete,    "Delete",    "Can remove records"),
    };

    public PermissionTreeQueryHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ModulePermissionTreeDto>> Handle(GetPermissionTreeRequest request, CancellationToken cancellationToken)
    {
        var modules = await LoadModulesAsync(cancellationToken);
        var services = await LoadServicesAsync(cancellationToken);

        return BuildTree(modules, services, rolePermissionLevelsByService: null);
    }

    public async Task<List<ModulePermissionTreeDto>?> Handle(GetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var roleExists = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RoleId, cancellationToken);

        if (!roleExists) return null;

        var modules = await LoadModulesAsync(cancellationToken);
        var services = await LoadServicesAsync(cancellationToken);

        var rolePermissionRows = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == request.RoleId)
            .Select(rp => new { rp.ServiceId, rp.Level })
            .ToListAsync(cancellationToken);

        // Highest granted level per service (defensive: a role may have multiple rows
        // on the same ServiceId in degenerate data — take max to stay safe under the
        // hierarchy rule).
        var maxLevelByService = rolePermissionRows
            .GroupBy(rp => rp.ServiceId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Level));

        return BuildTree(modules, services, maxLevelByService);
    }

    private async Task<List<ModuleRow>> LoadModulesAsync(CancellationToken cancellationToken) =>
        await _dbContext.Modules
            .AsNoTracking()
            .OrderBy(m => m.DisplayName)
            .Select(m => new ModuleRow(m.Id, m.ModuleKey, m.DisplayName))
            .ToListAsync(cancellationToken);

    private async Task<List<ServiceRow>> LoadServicesAsync(CancellationToken cancellationToken) =>
        await _dbContext.Services
            .AsNoTracking()
            .OrderBy(s => s.DisplayName)
            .Select(s => new ServiceRow(s.Id, s.ModuleId, s.DisplayName))
            .ToListAsync(cancellationToken);

    private static List<ModulePermissionTreeDto> BuildTree(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<ServiceRow> services,
        IReadOnlyDictionary<Guid, ActionLevel>? rolePermissionLevelsByService)
    {
        var servicesByModule = services
            .GroupBy(s => s.ModuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ModulePermissionTreeDto>(modules.Count);

        foreach (var m in modules)
        {
            var resourceDtos = new List<ResourcePermissionTreeDto>();

            if (servicesByModule.TryGetValue(m.Id, out var moduleServices))
            {
                foreach (var s in moduleServices)
                {
                    var canonicalResource = PermissionIdentity.ResourceFor(m.ModuleKey, s.DisplayName);
                    var grantedLevel = rolePermissionLevelsByService is not null
                        && rolePermissionLevelsByService.TryGetValue(s.Id, out var lvl)
                            ? lvl
                            : (ActionLevel?)null;

                    var permissions = new List<PermissionActionDto>(PermissionActions.Length);
                    foreach (var pa in PermissionActions)
                    {
                        permissions.Add(new PermissionActionDto
                        {
                            PermissionId = $"{s.Id}_{pa.Level}",
                            PermissionName = PermissionIdentity.Create(m.ModuleKey, canonicalResource, pa.Action),
                            Action = pa.Action,
                            Description = pa.Description,
                            IsAssigned = rolePermissionLevelsByService is null
                                ? null
                                : grantedLevel is { } gl && gl >= pa.Level,
                        });
                    }

                    resourceDtos.Add(new ResourcePermissionTreeDto
                    {
                        ResourceId = s.Id,
                        ResourceName = s.DisplayName,
                        Permissions = permissions,
                    });
                }
            }

            result.Add(new ModulePermissionTreeDto
            {
                ModuleId = m.Id,
                ModuleName = m.DisplayName,
                Resources = resourceDtos,
            });
        }

        return result;
    }

    private sealed record ModuleRow(Guid Id, string ModuleKey, string DisplayName);
    private sealed record ServiceRow(Guid Id, Guid ModuleId, string DisplayName);
}
