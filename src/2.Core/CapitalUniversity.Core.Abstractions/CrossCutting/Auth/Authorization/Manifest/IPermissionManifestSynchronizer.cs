using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Reconciles the in-memory <see cref="IPermissionManifestRegistry"/> against the
/// database <c>Modules</c> + <c>Resources</c> tables. Additive only — never
/// deletes rows the legacy seeder may have created for teammate modules. Safe
/// to call repeatedly; rows missing for a manifest-declared resource are
/// created, existing rows are left alone.
/// </summary>
public interface IPermissionManifestSynchronizer
{
    Task<PermissionSyncReport> SynchronizeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Diagnostic counts returned from one synchronization run.</summary>
public sealed record PermissionSyncReport(int ModulesCreated, int ResourcesCreated, int ManifestsProcessed);
