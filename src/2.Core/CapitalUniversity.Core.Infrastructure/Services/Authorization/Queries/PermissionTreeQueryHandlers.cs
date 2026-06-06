using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;

public class PermissionTreeQueryHandler : IPermissionTreeQueryHandler
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionManifestRegistry _registry;
    private readonly ILocalizationService _localization;
    private readonly IPermissionManagementService _permissionManagementService;
    private readonly ICacheService? _cache;
    private readonly PermissionCacheOptions _cacheOptions;

    public PermissionTreeQueryHandler(
        CoreDbContext dbContext,
        IPermissionManifestRegistry registry,
        ILocalizationService localization,
        IPermissionManagementService permissionManagementService,
        ICacheService? cache = null,
        PermissionCacheOptions? cacheOptions = null)
    {
        _dbContext = dbContext;
        _registry = registry;
        _localization = localization;
        _permissionManagementService = permissionManagementService;
        // Cache is optional: when absent (unit tests construct without it) the
        // handler falls back to direct DB reads. PermissionCacheOptions is bound
        // as IOptions<>, not as the bare type, so DI supplies null here — default
        // the TTL rather than NRE.
        _cache = cache;
        _cacheOptions = cacheOptions ?? new PermissionCacheOptions();
    }

    public async Task<List<ModulePermissionTreeDto>> Handle(GetPermissionTreeRequest request, CancellationToken cancellationToken)
    {
        var modules = await LoadModulesAsync(cancellationToken);
        var resources = await LoadResourcesAsync(cancellationToken);

        return BuildTree(modules, resources, grantedActionsByResource: null);
    }

    public async Task<List<ModulePermissionTreeDto>> Handle(GetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var roleExists = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RoleId, cancellationToken);

        if (!roleExists)
        {
            throw new NotFoundException("Role", request.RoleId);
        }

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

    public async Task<List<ModulePermissionTreeDto>> Handle(GetUserPermissionTreeRequest request, CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Staffs
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UserId, cancellationToken);

        if (!userExists)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var modules = await LoadModulesAsync(cancellationToken);
        var resources = await LoadResourcesAsync(cancellationToken);

        // This retrieves all effective permissions (roles + overrides) for the user.
        // It expands the roles and overrides into discrete effective actions.
        var effectivePermissions = await _permissionManagementService.GetEffectivePermissionsAsync(request.UserId, cancellationToken);

        // Group the scopes by permission (Module + Resource + Action)
        var scopesByPermission = effectivePermissions
            .GroupBy(p => PermissionIdentity.Create(p.Module, p.Resource, p.Action))
            .ToDictionary(g => g.Key, g => g.Select(p => p.Scope).ToList());

        // Fetch user overrides to flag provenance
        var userOverrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource).ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == request.UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allowOverrides = new HashSet<string>(StringComparer.Ordinal);
        var denyOverrides = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ov in userOverrides)
        {
            var identity = PermissionIdentity.Create(ov.Resource.Module.ModuleKey, ov.Resource.Key, ov.Action);
            if (ov.Type == CapitalUniversity.Core.Domain.Common.OverrideType.Allow)
                allowOverrides.Add(identity);
            else
                denyOverrides.Add(identity);
        }

        return BuildUserTree(modules, resources, scopesByPermission, allowOverrides, denyOverrides);
    }

    // Module/Resource rows are cached culture-NEUTRAL (DisplayName stays JSON);
    // localization runs per-request in BuildTree, so one cache entry serves every
    // culture. Keyed by the global permission epoch — a manifest sync rotates the
    // epoch (InvalidateAllAsync) and orphans these entries; the TTL is the
    // backstop if a deploy adds modules without rotating it. Stampede-protected so
    // a burst of admin tree loads after a cold cache runs one scan, not N.
    private async Task<List<ModuleRow>> LoadModulesAsync(CancellationToken cancellationToken)
    {
        if (_cache is null) return await QueryModulesAsync(cancellationToken);

        var epoch = await GetEpochAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<ModuleRow>>(
            $"perm_tree_modules_{epoch}",
            async ct => await QueryModulesAsync(ct),
            TimeSpan.FromMinutes(_cacheOptions.LookupTtlMinutes),
            cancellationToken);
        return cached ?? await QueryModulesAsync(cancellationToken);
    }

    private Task<List<ModuleRow>> QueryModulesAsync(CancellationToken cancellationToken) =>
        _dbContext.Modules
            .AsNoTracking()
            .OrderBy(m => m.OrderNumber)
            .Select(m => new ModuleRow(m.Id, m.ModuleKey, m.DisplayName))
            .ToListAsync(cancellationToken);

    private async Task<List<ResourceRow>> LoadResourcesAsync(CancellationToken cancellationToken)
    {
        if (_cache is null) return await QueryResourcesAsync(cancellationToken);

        var epoch = await GetEpochAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<ResourceRow>>(
            $"perm_tree_resources_{epoch}",
            async ct => await QueryResourcesAsync(ct),
            TimeSpan.FromMinutes(_cacheOptions.LookupTtlMinutes),
            cancellationToken);
        return cached ?? await QueryResourcesAsync(cancellationToken);
    }

    private Task<List<ResourceRow>> QueryResourcesAsync(CancellationToken cancellationToken) =>
        _dbContext.Resources
            .AsNoTracking()
            .OrderBy(r => r.OrderNumber)
            .Select(r => new ResourceRow(r.Id, r.ModuleId, r.Key, r.DisplayName))
            .ToListAsync(cancellationToken);

    // Reads the global epoch stamp (seeded/owned by PermissionManagementService);
    // defaults to "0" when missing so the key is stable before first seed.
    private async Task<string> GetEpochAsync(CancellationToken cancellationToken)
    {
        if (_cache is null) return "0";
        var v = await _cache.GetAsync<string>(PermissionCacheInvalidator.GlobalEpochKey, cancellationToken);
        return string.IsNullOrEmpty(v) ? "0" : v;
    }

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
                ? moduleResources
                    .Select(r => BuildResourceDto(m.ModuleKey, r, grantedActionsByResource))
                    .Where(r => r.Permissions.Count > 0)
                    .ToList()
                : new List<ResourcePermissionTreeDto>();

            if (resourceDtos.Count == 0) continue;

            result.Add(new ModulePermissionTreeDto
            {
                ModuleId = m.Id,
                // Module.DisplayName is persisted as the canonical
                // {"ar":"…","en":"…"} JSON shape (legacy plain-text rows pass
                // through as the literal). Decoding here is the single read
                // path for the permission-tree UI.
                ModuleName = _localization.Get<string>(m.DisplayName) ?? m.DisplayName,
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
            ResourceName = _localization.Get<string>(resource.DisplayName) ?? resource.DisplayName,
            Permissions = permissions,
        };
    }

    private List<ModulePermissionTreeDto> BuildUserTree(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<ResourceRow> resources,
        Dictionary<string, List<PermissionScopeDto>> scopesByPermission,
        HashSet<string> allowOverrides,
        HashSet<string> denyOverrides)
    {
        var resourcesByModule = resources
            .GroupBy(r => r.ModuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ModulePermissionTreeDto>(modules.Count);

        foreach (var m in modules)
        {
            var resourceDtos = resourcesByModule.TryGetValue(m.Id, out var moduleResources)
                ? moduleResources
                    .Select(r => BuildUserResourceDto(m.ModuleKey, r, scopesByPermission, allowOverrides, denyOverrides))
                    .Where(r => r.Permissions.Count > 0)
                    .ToList()
                : new List<ResourcePermissionTreeDto>();

            if (resourceDtos.Count == 0) continue;

            result.Add(new ModulePermissionTreeDto
            {
                ModuleId = m.Id,
                ModuleName = _localization.Get<string>(m.DisplayName) ?? m.DisplayName,
                Resources = resourceDtos,
            });
        }

        return result;
    }

    private ResourcePermissionTreeDto BuildUserResourceDto(
        string moduleKey,
        ResourceRow resource,
        Dictionary<string, List<PermissionScopeDto>> scopesByPermission,
        HashSet<string> allowOverrides,
        HashSet<string> denyOverrides)
    {
        var resourceDef = _registry.GetResource(moduleKey, resource.Key);
        var actions = resourceDef?.Actions ?? Array.Empty<ActionDefinition>();

        var permissions = actions
            .OrderBy(a => a.DisplayOrder ?? a.OrderNumber)
            .Select(action =>
            {
                var permissionIdentity = PermissionIdentity.Create(moduleKey, resource.Key, action.Name);
                var isAssigned = scopesByPermission.TryGetValue(permissionIdentity, out var scopes);

                return new PermissionActionDto
                {
                    PermissionId = $"{resource.Id}_{action.Name}",
                    PermissionName = permissionIdentity,
                    Action = action.Name,
                    Description = DescribeAction(action),
                    IsAssigned = isAssigned,
                    Scopes = isAssigned ? scopes : null,
                    HasAllowOverride = allowOverrides.Contains(permissionIdentity),
                    HasDenyOverride = denyOverrides.Contains(permissionIdentity)
                };
            })
            .ToList();

        return new ResourcePermissionTreeDto
        {
            ResourceId = resource.Id,
            ResourceName = _localization.Get<string>(resource.DisplayName) ?? resource.DisplayName,
            Permissions = permissions,
        };
    }

    /// <summary>
    /// Render a human-readable description for an action verb. CRUD verbs
    /// resolve through dedicated catalog keys; non-CRUD module-declared
    /// verbs (e.g. <c>Approve</c>, <c>Export</c>) fall back to the generic
    /// "Can perform this action" key.
    /// </summary>
    private string DescribeAction(ActionDefinition action) => action.Name switch
    {
        "View"      => _localization.GetString(LocalizedKeys.Permissions.ActionDescription.View),
        "Insert"    => _localization.GetString(LocalizedKeys.Permissions.ActionDescription.Insert),
        "EditClose" => _localization.GetString(LocalizedKeys.Permissions.ActionDescription.EditClose),
        "Open"      => _localization.GetString(LocalizedKeys.Permissions.ActionDescription.Open),
        "Delete"    => _localization.GetString(LocalizedKeys.Permissions.ActionDescription.Delete),
        _           => _localization.GetString(LocalizedKeys.Permissions.ActionDescriptionFallback),
    };

    // internal (not private) so System.Text.Json can round-trip them through Redis.
    internal sealed record ModuleRow(Guid Id, string ModuleKey, string DisplayName);
    internal sealed record ResourceRow(Guid Id, Guid ModuleId, string Key, string DisplayName);
}
