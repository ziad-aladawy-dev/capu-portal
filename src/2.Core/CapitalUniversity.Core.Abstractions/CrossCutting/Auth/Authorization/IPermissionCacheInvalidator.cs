using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Single seam any code path can call to drop cached authorization state. Rotates
/// per-user / per-role version stamps — the cache itself never enumerates keys,
/// stale entries are simply orphaned by the next version-mismatched lookup.
///
/// <para>
/// Three layers, finest-grained first:
/// <list type="bullet">
///   <item><see cref="InvalidateUserAsync"/> — one user (logout, password change, individual override edit).</item>
///   <item><see cref="InvalidateRoleAsync"/> — every staff currently holding that role
///       (role-grant or role-permission edits).</item>
///   <item><see cref="InvalidateAllAsync"/> — global epoch bump (manifest sync,
///       schema migration, blast-radius drill).</item>
/// </list>
/// </para>
/// </summary>
public interface IPermissionCacheInvalidator
{
    Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
