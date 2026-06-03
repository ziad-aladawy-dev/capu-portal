using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

public class PermissionManifestRegistry : IPermissionManifestRegistry
{
    private readonly IReadOnlyCollection<IPermissionManifest> _manifests;
    private readonly HashSet<string> _canonicalSet;
    private readonly Dictionary<(string Module, string Resource), ResourceDefinition> _resourcesByKey;

    public PermissionManifestRegistry(IEnumerable<IPermissionManifest> manifests)
    {
        _manifests = manifests?.ToList() ?? new List<IPermissionManifest>();
        ValidateOrThrow(_manifests);

        var canonical = new List<string>();
        _resourcesByKey = new Dictionary<(string, string), ResourceDefinition>();
        foreach (var m in _manifests)
        {
            foreach (var r in m.Resources)
            {
                _resourcesByKey[(m.Module, r.Key)] = r;
                foreach (var a in r.Actions)
                {
                    canonical.Add(r.CanonicalName(m.Module, a.Name));
                }
            }
        }

        AllCanonicalNames = canonical;
        _canonicalSet = new HashSet<string>(canonical, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IPermissionManifest> Manifests => _manifests;
    public IReadOnlyCollection<string> AllCanonicalNames { get; }

    public bool Contains(string canonicalName) => _canonicalSet.Contains(canonicalName);

    public ResourceDefinition? GetResource(string module, string resourceKey) =>
        _resourcesByKey.TryGetValue((module, resourceKey), out var def) ? def : null;

    private static void ValidateOrThrow(IReadOnlyCollection<IPermissionManifest> manifests)
    {
        var seenModules = new HashSet<string>(StringComparer.Ordinal);
        var seenCanonical = new HashSet<string>(StringComparer.Ordinal);

        foreach (var manifest in manifests)
        {
            ValidateManifestKey(manifest, seenModules);
            ValidateManifestResources(manifest, seenCanonical);
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

    private static void ValidateManifestResources(IPermissionManifest manifest, HashSet<string> seenCanonical)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in manifest.Resources)
        {
            ValidateResourceFields(manifest.Module, resource);

            if (!seenKeys.Add(resource.Key))
                throw new InvalidOperationException(
                    $"Manifest '{manifest.Module}' declares duplicate resource key '{resource.Key}'.");

            var seenActions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in resource.Actions)
            {
                if (string.IsNullOrWhiteSpace(action.Name))
                    throw new InvalidOperationException(
                        $"Manifest '{manifest.Module}' resource '{resource.Key}' declares an action with an empty name.");

                if (!seenActions.Add(action.Name))
                    throw new InvalidOperationException(
                        $"Manifest '{manifest.Module}' resource '{resource.Key}' declares duplicate action '{action.Name}'.");

                var canonical = resource.CanonicalName(manifest.Module, action.Name);
                if (!seenCanonical.Add(canonical))
                    throw new InvalidOperationException(
                        $"Duplicate canonical permission name '{canonical}' — two manifests claim the same identity.");
            }

            ValidateImpliesGraph(manifest.Module, resource, seenActions);
        }
    }

    private static void ValidateImpliesGraph(string module, ResourceDefinition resource, HashSet<string> declaredActions)
    {
        foreach (var action in resource.Actions)
        {
            foreach (var implied in action.Implies)
            {
                if (string.IsNullOrWhiteSpace(implied))
                    throw new InvalidOperationException(
                        $"Manifest '{module}' resource '{resource.Key}' action '{action.Name}' declares an empty implied action name.");
                if (!declaredActions.Contains(implied))
                    throw new InvalidOperationException(
                        $"Manifest '{module}' resource '{resource.Key}' action '{action.Name}' implies '{implied}', which is not a declared action on this resource.");
                if (string.Equals(implied, action.Name, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Manifest '{module}' resource '{resource.Key}' action '{action.Name}' implies itself.");
            }
        }
    }

    private static void ValidateResourceFields(string module, ResourceDefinition resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Key))
            throw new InvalidOperationException(
                $"Manifest '{module}' declares a resource with an empty Key.");
        if (string.IsNullOrWhiteSpace(resource.DisplayName))
            throw new InvalidOperationException(
                $"Manifest '{module}' resource '{resource.Key}' has an empty DisplayName.");
        if (resource.Actions is null || resource.Actions.Count == 0)
            throw new InvalidOperationException(
                $"Manifest '{module}' resource '{resource.Key}' declares no Actions.");
    }
}
