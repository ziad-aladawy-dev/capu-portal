using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

public class PermissionManifestRegistry : IPermissionManifestRegistry
{
    private readonly IReadOnlyCollection<IPermissionManifest> _manifests;
    private readonly HashSet<string> _canonicalSet;

    public PermissionManifestRegistry(IEnumerable<IPermissionManifest> manifests)
    {
        _manifests = manifests?.ToList() ?? new List<IPermissionManifest>();
        ValidateOrThrow(_manifests);

        var canonical = new List<string>();
        foreach (var m in _manifests)
        {
            foreach (var p in m.Permissions)
            {
                canonical.Add(p.CanonicalName(m.Module));
            }
        }

        AllCanonicalNames = canonical;
        _canonicalSet = new HashSet<string>(canonical, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IPermissionManifest> Manifests => _manifests;
    public IReadOnlyCollection<string> AllCanonicalNames { get; }

    public bool Contains(string canonicalName) => _canonicalSet.Contains(canonicalName);

    private static void ValidateOrThrow(IReadOnlyCollection<IPermissionManifest> manifests)
    {
        var seenModules = new HashSet<string>(StringComparer.Ordinal);
        var seenCanonical = new HashSet<string>(StringComparer.Ordinal);

        foreach (var manifest in manifests)
        {
            ValidateManifestKey(manifest, seenModules);
            ValidateManifestPermissions(manifest, seenCanonical);
        }
    }

    private static void ValidateManifestKey(IPermissionManifest manifest, HashSet<string> seenModules)
    {
        if (string.IsNullOrWhiteSpace(manifest.Module))
            throw new InvalidOperationException(
                $"Permission manifest {manifest.GetType().FullName} declares a null/empty Module key.");

        if (!seenModules.Add(manifest.Module))
            throw new InvalidOperationException(
                $"Duplicate IPermissionManifest module key '{manifest.Module}'. Each module key must be owned by exactly one manifest.");
    }

    private static void ValidateManifestPermissions(IPermissionManifest manifest, HashSet<string> seenCanonical)
    {
        var seenLocal = new HashSet<(string, string)>();
        foreach (var perm in manifest.Permissions)
        {
            ValidatePermissionFields(manifest.Module, perm);

            if (!seenLocal.Add((perm.Resource, perm.Action)))
                throw new InvalidOperationException(
                    $"Manifest '{manifest.Module}' declares duplicate permission '{perm.Resource}.{perm.Action}'.");

            var canonical = perm.CanonicalName(manifest.Module);
            if (!seenCanonical.Add(canonical))
                throw new InvalidOperationException(
                    $"Duplicate canonical permission name '{canonical}' — two manifests claim the same identity.");
        }
    }

    private static void ValidatePermissionFields(string module, PermissionDefinition perm)
    {
        if (string.IsNullOrWhiteSpace(perm.Resource))
            throw new InvalidOperationException(
                $"Manifest '{module}' declares a permission with an empty Resource.");
        if (string.IsNullOrWhiteSpace(perm.Action))
            throw new InvalidOperationException(
                $"Manifest '{module}' declares a permission with an empty Action (resource='{perm.Resource}').");
        if (string.IsNullOrWhiteSpace(perm.DisplayName))
            throw new InvalidOperationException(
                $"Manifest '{module}' declares a permission without a DisplayName ({perm.Resource}.{perm.Action}).");
    }
}
