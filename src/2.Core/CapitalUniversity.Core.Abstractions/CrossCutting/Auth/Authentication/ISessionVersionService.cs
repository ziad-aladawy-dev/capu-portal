using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Read + bump the user's current SessionVersion. Concrete impl lives in Infrastructure
/// (DB-backed). The auth middleware reads it on every request; logout / password change
/// bump it to invalidate every outstanding token without keeping a blocklist.
/// </summary>
public interface ISessionVersionService
{
    /// <summary>
    /// Current SessionVersion for the user, or null if no Staff/Student row exists.
    /// </summary>
    Task<int?> GetCurrentVersionAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the user's SessionVersion. Returns the new value, or null
    /// if the user does not exist.
    /// </summary>
    Task<int?> IncrementVersionAsync(Guid userId, CancellationToken cancellationToken = default);
}
