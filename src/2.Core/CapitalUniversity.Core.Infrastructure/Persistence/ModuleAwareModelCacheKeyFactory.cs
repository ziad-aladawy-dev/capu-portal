using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

/// <summary>
/// EF caches one model per <see cref="DbContext"/> type, built the first time a
/// context of that type is used. <see cref="CoreDbContext"/> contributes
/// module-owned entities at model-creating time by scanning
/// <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> — a static list that
/// modules append to. If a context is built before a module registers its
/// assembly (common in parallel test runs where fixtures construct contexts in
/// arbitrary order), the cached model omits that module's entities and every
/// later context reuses it — surfacing as
/// <c>"Cannot create a DbSet for 'ScheduleSlot'"</c>.
///
/// <para>
/// Folding the registered assembly set into the cache key makes EF rebuild the
/// model whenever that set changes, so registration order no longer matters. In
/// production the set is fixed at startup before the first context is created, so
/// the model is still built exactly once.
/// </para>
/// </summary>
public sealed class ModuleAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        var assemblyKey = CoreDbContext.ModuleConfigurationAssemblies
            .Select(a => a.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Aggregate(0, (hash, name) => HashCode.Combine(hash, name));

        return (context.GetType(), designTime, assemblyKey);
    }

    public object Create(DbContext context) => Create(context, designTime: false);
}
