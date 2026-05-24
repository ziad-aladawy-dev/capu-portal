using System.Reflection;
using System.Runtime.CompilerServices;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.UniTests._TestInfra;

/// <summary>
/// Thread-safe check-then-add for <see cref="CoreDbContext.ModuleConfigurationAssemblies"/>
/// from DB-backed test fixtures, PLUS an eager pre-registration step so EF's
/// process-wide model cache locks in a complete module set on first use.
///
/// <para>
/// Why both?
/// </para>
/// <para>
/// The underlying list is a plain <c>List&lt;Assembly&gt;</c> shared across
/// every <see cref="CoreDbContext"/> instance in the process. In production,
/// every module's <c>AddXxxModule()</c> DI extension adds its assembly at
/// startup, BEFORE the first request creates a context. In tests, fixtures
/// register lazily inside their constructors. EF Core caches the IModel per
/// DbContext type within its default service provider — once any test
/// creates a <see cref="CoreDbContext"/>, that cached model is reused
/// across the entire process. If the cached model was built from a partial
/// assembly list, every later fixture sees the partial model regardless of
/// what its ctor adds to the list.
/// </para>
/// <para>
/// The <see cref="EagerRegisterAllModules"/> method below runs at assembly
/// load time via <see cref="ModuleInitializerAttribute"/>, BEFORE xUnit
/// constructs any test class. It pre-loads every known module assembly so
/// the very first DbContext built in the process has the complete set.
/// Concurrency tests are deterministic from that point on.
/// </para>
/// <para>
/// <see cref="Ensure"/> stays for backward compatibility — existing tests
/// still call it in their ctors. It is now a no-op for the four
/// known modules and continues to safely register anything else.
/// </para>
/// </summary>
internal static class ModuleAssemblyRegistration
{
    private static readonly object Gate = new();

    /// <summary>
    /// Runs once at test-assembly load, before any test fixture constructor.
    /// Pre-populates the static module-assemblies list so EF's model cache
    /// is seeded with every module's <see cref="IEntityTypeConfiguration{T}"/>
    /// before any thread can observe a partial model. Without this, parallel
    /// fixtures racing on the static list produced flaky
    /// "Cannot create a DbSet for X because this type is not in the model"
    /// failures in `ScheduleSlotRepositoryDbTests` and
    /// `CourseOfferingRepositoryDbTests`.
    /// </summary>
    [ModuleInitializer]
    internal static void EagerRegisterAllModules()
    {
        // One representative type per module assembly. We could enumerate
        // AppDomain.CurrentDomain.GetAssemblies() but explicit references
        // make the dependency visible at compile time.
        Ensure(typeof(CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering).Assembly);
        Ensure(typeof(CapitalUniversity.Modules.Schedule.Domain.ScheduleSlot).Assembly);
        Ensure(typeof(CapitalUniversity.Modules.Payments.Domain.Invoice).Assembly);
        Ensure(typeof(CapitalUniversity.Modules.Student.Domain.StudentProfileRecord).Assembly);
    }

    /// <summary>
    /// Ensure <paramref name="moduleAssembly"/> is registered with the
    /// <see cref="CoreDbContext"/>'s module-configuration list exactly once.
    /// Safe to call from any thread; idempotent. After
    /// <see cref="EagerRegisterAllModules"/> has run, calls for the four
    /// known modules are no-ops.
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
