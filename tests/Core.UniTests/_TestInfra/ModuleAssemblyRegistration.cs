using System.Reflection;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.UniTests._TestInfra;

/// <summary>
/// Thread-safe check-then-add for <see cref="CoreDbContext.ModuleConfigurationAssemblies"/>
/// from DB-backed test fixtures.
///
/// <para>
/// The underlying list is a plain <c>List&lt;Assembly&gt;</c> shared across
/// every <see cref="CoreDbContext"/> instance in the process. When two test
/// fixture ctors run on different threads (xUnit parallel collections,
/// Stryker's parallel runners) they race on <c>Contains</c> +
/// <c>Add</c> — and worse, on the foreach inside <c>OnModelCreating</c> if
/// another ctor mutates the list while EF is iterating it. Both cause flaky
/// "Collection was modified" failures that historically blanked out repository
/// mutation scores (Stryker counted them as NoCoverage).
/// </para>
///
/// <para>
/// One process-wide lock here keeps the production code untouched and the
/// fixtures one line lighter than inlining a lock per file.
/// </para>
/// </summary>
internal static class ModuleAssemblyRegistration
{
    private static readonly object Gate = new();

    /// <summary>
    /// Ensure <paramref name="moduleAssembly"/> is registered with the
    /// <see cref="CoreDbContext"/>'s module-configuration list exactly once.
    /// Safe to call from any thread; idempotent.
    /// </summary>
    public static void Ensure(Assembly moduleAssembly)
    {
        lock (Gate)
        {
            if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
            {
                CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
            }
        }
    }
}
