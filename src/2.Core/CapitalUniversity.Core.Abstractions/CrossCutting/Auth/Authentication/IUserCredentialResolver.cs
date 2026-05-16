using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IUserCredentialResolver
{
    Task<IUserCredential?> ResolveCredentialAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the credential by user id. Used by refresh-token issuance to read the
    /// current SessionVersion + scope info without requiring the caller's identifier.
    /// </summary>
    Task<IUserCredential?> ResolveByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the current password, sets the new hash, persists. Returns true on
    /// success. SessionVersion bump is intentionally handled by the caller.
    /// </summary>
    Task<bool> UpdatePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        IPasswordHasher hasher,
        CancellationToken cancellationToken = default);
}
