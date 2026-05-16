using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;

/// <summary>
/// Additive synchroniser: ensures the <c>Modules</c> and <c>Services</c> tables hold
/// a row for every permission declared by every <see cref="IPermissionManifest"/>.
/// Existing rows are matched by their natural keys (<c>ModuleKey</c> for Modules,
/// <c>(ModuleId, DisplayName)</c> for Services) and never deleted or renamed — so
/// rows seeded by teammate-owned legacy seeders remain untouched.
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
        // Snapshot existing rows once. The synchroniser is expected to run during
        // startup and bulk operations dominate over individual EF roundtrips.
        var existingModules = await _dbContext.Modules.ToListAsync(cancellationToken);
        var moduleByKey = existingModules.ToDictionary(m => m.ModuleKey, StringComparer.Ordinal);
        int modulesCreated = 0;

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
            }
        }

        // Flush so newly-created Module rows have stable Ids before we touch Services.
        if (modulesCreated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingServices = await _dbContext.Services.ToListAsync(cancellationToken);
        var servicesByModuleAndName = existingServices
            .GroupBy(s => s.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(s => s.DisplayName, StringComparer.Ordinal));
        int servicesCreated = 0;

        foreach (var manifest in _registry.Manifests)
        {
            var moduleRow = moduleByKey[manifest.Module];

            if (!servicesByModuleAndName.TryGetValue(moduleRow.Id, out var byName))
            {
                byName = new Dictionary<string, Service>(StringComparer.Ordinal);
                servicesByModuleAndName[moduleRow.Id] = byName;
            }

            foreach (var perm in manifest.Permissions)
            {
                if (byName.ContainsKey(perm.DisplayName)) continue;

                var row = new Service
                {
                    Id = Guid.NewGuid(),
                    ModuleId = moduleRow.Id,
                    DisplayName = perm.DisplayName,
                    OrderNumber = perm.OrderNumber
                };
                _dbContext.Services.Add(row);
                byName[perm.DisplayName] = row;
                servicesCreated++;
            }
        }

        if (servicesCreated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PermissionSyncReport(
            ModulesCreated: modulesCreated,
            ServicesCreated: servicesCreated,
            ManifestsProcessed: _registry.Manifests.Count);
    }
}
