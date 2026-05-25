using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;

/// <summary>
/// Reconciles the in-memory <see cref="IPermissionManifestRegistry"/> against
/// the database <c>Modules</c> + <c>Resources</c> tables.
/// <para>
/// Matching is by natural key (<c>ModuleKey</c> for Modules, <c>(ModuleId, Key)</c>
/// for Resources). On match, presentation metadata that's safe to refresh
/// (<c>DisplayName</c>, <c>Icon</c>, <c>OrderNumber</c>) is updated from the
/// manifest so a rename in code propagates without orphaning the row's FKs.
/// Rows are never deleted — a manifest authors a resource key for life.
/// </para>
/// </summary>
public class PermissionManifestSynchronizer : IPermissionManifestSynchronizer
{
    private readonly CoreDbContext _dbContext;
    private readonly IPermissionManifestRegistry _registry;

    public PermissionManifestSynchronizer(CoreDbContext dbContext, IPermissionManifestRegistry registry)
    {
        _dbContext = dbContext;
        _registry = registry;
    }

    public async Task<PermissionSyncReport> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var existingModules = await _dbContext.Modules.ToListAsync(cancellationToken);
        var moduleByKey = existingModules.ToDictionary(m => m.ModuleKey, StringComparer.Ordinal);
        int modulesCreated = 0;
        int modulesUpdated = 0;

        foreach (var manifest in _registry.Manifests)
        {
            if (!moduleByKey.TryGetValue(manifest.Module, out var moduleRow))
            {
                moduleRow = new Module
                {
                    Id = Guid.NewGuid(),
                    ModuleKey = manifest.Module,
                    DisplayName = manifest.DisplayName,
                    Icon = manifest.Icon,
                    OrderNumber = manifest.OrderNumber
                };
                _dbContext.Modules.Add(moduleRow);
                moduleByKey[manifest.Module] = moduleRow;
                modulesCreated++;
                continue;
            }

            if (RefreshModuleMetadata(moduleRow, manifest))
            {
                modulesUpdated++;
            }
        }

        if (modulesCreated > 0 || modulesUpdated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingResources = await _dbContext.Resources.ToListAsync(cancellationToken);
        var resourcesByModuleAndKey = existingResources
            .GroupBy(r => r.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(r => r.Key, StringComparer.Ordinal));
        int resourcesCreated = 0;
        int resourcesUpdated = 0;

        int resourcesRenamed = 0;
        foreach (var manifest in _registry.Manifests)
        {
            var moduleRow = moduleByKey[manifest.Module];

            if (!resourcesByModuleAndKey.TryGetValue(moduleRow.Id, out var byKey))
            {
                byKey = new Dictionary<string, Resource>(StringComparer.Ordinal);
                resourcesByModuleAndKey[moduleRow.Id] = byKey;
            }

            foreach (var resource in manifest.Resources)
            {
                if (byKey.TryGetValue(resource.Key, out var existingRow))
                {
                    if (RefreshResourceMetadata(existingRow, resource))
                    {
                        resourcesUpdated++;
                    }
                    continue;
                }

                // M13 — rename detection. When the manifest declares
                // PreviousKeys and one of them matches an existing row under
                // the same module, treat the new declaration as a rename:
                // update the row's Key (FK pointers from RolePermission /
                // StaffPermissionOverride survive automatically because they
                // reference ResourceId, not Key) and refresh metadata. Grants
                // remain valid; admins do not silently lose permissions on
                // the new key.
                Resource? renamed = null;
                if (resource.PreviousKeys is { Count: > 0 })
                {
                    foreach (var previousKey in resource.PreviousKeys)
                    {
                        if (byKey.TryGetValue(previousKey, out var legacyRow))
                        {
                            renamed = legacyRow;
                            break;
                        }
                    }
                }

                if (renamed is not null)
                {
                    byKey.Remove(renamed.Key);
                    renamed.Key = resource.Key;
                    RefreshResourceMetadata(renamed, resource);
                    byKey[renamed.Key] = renamed;
                    resourcesRenamed++;
                    continue;
                }

                var row = new Resource
                {
                    Id = Guid.NewGuid(),
                    ModuleId = moduleRow.Id,
                    Key = resource.Key,
                    DisplayName = resource.DisplayName,
                    OrderNumber = resource.OrderNumber
                };
                _dbContext.Resources.Add(row);
                byKey[resource.Key] = row;
                resourcesCreated++;
            }
        }

        if (resourcesCreated > 0 || resourcesUpdated > 0 || resourcesRenamed > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PermissionSyncReport(
            ModulesCreated: modulesCreated,
            ResourcesCreated: resourcesCreated,
            ManifestsProcessed: _registry.Manifests.Count);
    }

    private static bool RefreshModuleMetadata(Module row, IPermissionManifest manifest)
    {
        var dirty = false;
        if (!string.Equals(row.DisplayName, manifest.DisplayName, StringComparison.Ordinal))
        {
            row.DisplayName = manifest.DisplayName;
            dirty = true;
        }
        if (!string.Equals(row.Icon, manifest.Icon, StringComparison.Ordinal))
        {
            row.Icon = manifest.Icon;
            dirty = true;
        }
        if (row.OrderNumber != manifest.OrderNumber)
        {
            row.OrderNumber = manifest.OrderNumber;
            dirty = true;
        }
        return dirty;
    }

    private static bool RefreshResourceMetadata(Resource row, ResourceDefinition def)
    {
        var dirty = false;
        if (!string.Equals(row.DisplayName, def.DisplayName, StringComparison.Ordinal))
        {
            row.DisplayName = def.DisplayName;
            dirty = true;
        }
        if (row.OrderNumber != def.OrderNumber)
        {
            row.OrderNumber = def.OrderNumber;
            dirty = true;
        }
        return dirty;
    }
}
