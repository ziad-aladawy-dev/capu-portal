namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Read-only view over every discovered <see cref="IPermissionManifest"/>. The
/// registry validates eagerly at construction: duplicate module keys, duplicate
/// <c>(Resource, Action)</c> pairs within a manifest, and malformed
/// (whitespace / empty) names all surface as a startup-time <see cref="System.InvalidOperationException"/>.
/// </summary>
public interface IPermissionManifestRegistry
{
    IReadOnlyCollection<IPermissionManifest> Manifests { get; }

    /// <summary>Every declared permission flattened, in <c>{module}.{resource}.{action}</c> form.</summary>
    IReadOnlyCollection<string> AllCanonicalNames { get; }

    /// <summary>True iff a manifest declares the given canonical name.</summary>
    bool Contains(string canonicalName);
}
