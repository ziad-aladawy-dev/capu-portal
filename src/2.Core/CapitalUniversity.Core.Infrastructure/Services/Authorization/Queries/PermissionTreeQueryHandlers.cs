using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;

public class PermissionTreeQueryHandler : IPermissionTreeQueryHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionManifestRegistry _registry;

    public PermissionTreeQueryHandler(CoreDbContext dbContext, IPermissionManifestRegistry registry)
    {
        _dbContext = dbContext;
        _registry = registry;
    }

    public async Task<List<ModulePermissionTreeDto>> Handle(GetPermissionTreeRequest request, CancellationToken cancellationToken)
    {
        var modules = await LoadModulesAsync(cancellationToken);
        var resources = await LoadResourcesAsync(cancellationToken);

        return BuildTree(modules, resources, grantedActionsByResource: null);
    }

    public async Task<List<ModulePermissionTreeDto>?> Handle(GetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var roleExists = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RoleId, cancellationToken);

        if (!roleExists) return null;

        var modules = await LoadModulesAsync(cancellationToken);
        var resources = await LoadResourcesAsync(cancellationToken);

        var rolePermissionRows = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == request.RoleId)
            .Select(rp => new { rp.ResourceId, rp.Action })
            .ToListAsync(cancellationToken);

        var grantedActionsByResource = rolePermissionRows
            .GroupBy(rp => rp.ResourceId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Action).ToHashSet(StringComparer.Ordinal));

        return BuildTree(modules, resources, grantedActionsByResource);
    }

    private async Task<List<ModuleRow>> LoadModulesAsync(CancellationToken cancellationToken) =>
        await _dbContext.Modules
            .AsNoTracking()
            .OrderBy(m => m.DisplayName)
            .Select(m => new ModuleRow(m.Id, m.ModuleKey, m.DisplayName))
            .ToListAsync(cancellationToken);

    private async Task<List<ResourceRow>> LoadResourcesAsync(CancellationToken cancellationToken) =>
        await _dbContext.Resources
            .AsNoTracking()
            .OrderBy(r => r.DisplayName)
            .Select(r => new ResourceRow(r.Id, r.ModuleId, r.Key, r.DisplayName))
            .ToListAsync(cancellationToken);

    private List<ModulePermissionTreeDto> BuildTree(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<ResourceRow> resources,
        Dictionary<Guid, HashSet<string>>? grantedActionsByResource)
    {
        var resourcesByModule = resources
            .GroupBy(r => r.ModuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ModulePermissionTreeDto>(modules.Count);

        foreach (var m in modules)
        {
            var resourceDtos = resourcesByModule.TryGetValue(m.Id, out var moduleResources)
                ? moduleResources.Select(r => BuildResourceDto(m.ModuleKey, r, grantedActionsByResource)).ToList()
                : new List<ResourcePermissionTreeDto>();

            result.Add(new ModulePermissionTreeDto
            {
                ModuleId = m.Id,
                ModuleName = m.DisplayName,
                Resources = resourceDtos,
            });
        }

        return result;
    }

    private ResourcePermissionTreeDto BuildResourceDto(
        string moduleKey,
        ResourceRow resource,
        Dictionary<Guid, HashSet<string>>? grantedActionsByResource)
    {
        var resourceDef = _registry.GetResource(moduleKey, resource.Key);
        var actions = resourceDef?.Actions ?? Array.Empty<ActionDefinition>();

        var grantedActions = grantedActionsByResource is not null
            && grantedActionsByResource.TryGetValue(resource.Id, out var set)
                ? set
                : null;

        var permissions = actions
            .OrderBy(a => a.DisplayOrder ?? a.OrderNumber)
            .Select(action => new PermissionActionDto
            {
                PermissionId = $"{resource.Id}_{action.Name}",
                PermissionName = PermissionIdentity.Create(moduleKey, resource.Key, action.Name),
                Action = action.Name,
                Description = DescribeAction(action),
                IsAssigned = grantedActionsByResource is null
                    ? null
                    : grantedActions is not null && grantedActions.Contains(action.Name),
            })
            .ToList();

        return new ResourcePermissionTreeDto
        {
            ResourceId = resource.Id,
            ResourceName = resource.DisplayName,
            Permissions = permissions,
        };
    }

    private static string DescribeAction(ActionDefinition action) => action.Name switch
    {
        "View"      => "Can view records",
        "Insert"    => "Can add new records",
        "EditClose" => "Can edit existing records",
        "Open"      => "Can open/unlock records",
        "Delete"    => "Can remove records",
        _ => action.Name,
    };

    private sealed record ModuleRow(Guid Id, string ModuleKey, string DisplayName);
    private sealed record ResourceRow(Guid Id, Guid ModuleId, string Key, string DisplayName);
}
