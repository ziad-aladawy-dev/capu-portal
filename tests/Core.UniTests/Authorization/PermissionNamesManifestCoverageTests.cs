using System.Reflection;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// Beyond the source-scan in <c>PermissionNamesCoverageTests</c>, this guard
/// asserts the inverse: every constant on <see cref="PermissionNames"/>
/// resolves to a canonical name that the aggregated manifest registry
/// declares. Catches drift in the other direction — a constant added without
/// a matching manifest entry would otherwise compile and pass the
/// source-scan, then fail in production when the DB has no Resource row.
/// </summary>
public class PermissionNamesManifestCoverageTests
{
    [Fact]
    public void EveryPermissionNamesConstant_IsDeclaredByAManifest()
    {
        var registry = new PermissionManifestRegistry(DiscoverAllManifests());
        var manifestNames = new HashSet<string>(registry.AllCanonicalNames, StringComparer.Ordinal);

        manifestNames.Should().NotBeEmpty("the test must discover at least one manifest");

        var constants = CollectPermissionNameConstants();
        constants.Should().NotBeEmpty();

        var missing = constants
            .Where(c => !manifestNames.Contains(c))
            .ToList();

        missing.Should().BeEmpty(
            "every PermissionNames constant must round-trip against a declared manifest entry; " +
            "missing: " + string.Join(", ", missing));
    }

    /// <summary>
    /// Walks every <see cref="IPermissionManifest"/> implementation across the
    /// loaded assemblies so the discovery matches what runtime DI produces.
    /// Module.* assemblies aren't auto-loaded for test bin folders — touch a
    /// type from each to force the load.
    /// </summary>
    private static IEnumerable<IPermissionManifest> DiscoverAllManifests()
    {
        ForceLoadModuleManifestAssemblies();

        var manifests = new List<IPermissionManifest>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Test assemblies can host stub manifests with intentionally empty
            // Module keys or duplicate module keys (used to exercise registry
            // validation). Skip them — we only want production manifests here.
            var name = asm.GetName().Name ?? string.Empty;
            if (name.Contains("Tests", StringComparison.Ordinal)
                || name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("System", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.OfType<Type>().ToArray(); }

            foreach (var type in types)
            {
                if (type is null || type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IPermissionManifest).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) is null) continue;
                var instance = (IPermissionManifest)Activator.CreateInstance(type)!;
                if (string.IsNullOrWhiteSpace(instance.Module)) continue;
                manifests.Add(instance);
            }
        }
        return manifests;
    }

    private static void ForceLoadModuleManifestAssemblies()
    {
        // Reference one type from each module's abstractions assembly so the
        // CLR loads it. The actual symbols pulled don't matter — the assembly
        // load is what enables reflection discovery below.
        _ = typeof(CapitalUniversity.Modules.CourseOffering.Abstractions.Manifest.CourseOfferingPermissionManifest);
        _ = typeof(CapitalUniversity.Modules.Payments.Abstractions.Manifest.PaymentsPermissionManifest);
        _ = typeof(CapitalUniversity.Modules.Schedule.Abstractions.Manifest.SchedulePermissionManifest);
        _ = typeof(CapitalUniversity.Modules.Student.Abstractions.Manifest.StudentInformationPermissionManifest);
        _ = typeof(CapitalUniversity.Modules.StudentServices.Abstractions.Manifest.StudentServicesPermissionManifest);
    }

    private static HashSet<string> CollectPermissionNameConstants()
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nested in typeof(PermissionNames).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (!field.IsLiteral || field.IsInitOnly) continue;
                if (field.FieldType != typeof(string)) continue;
                if (field.GetRawConstantValue() is string v) values.Add(v);
            }
        }
        return values;
    }
}