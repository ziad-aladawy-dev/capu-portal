using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Refresh-token lifecycle: issue, validate, rotate, revoke. Implementations
/// hash the raw token before persisting (raw value only ever exists in the
/// response to the caller).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Mints a new refresh token for the user, hashes &amp; persists it, returns the raw
    /// value the caller should hand back on the next refresh. The raw value is only
    /// observable here.
    /// </summary>
    Task<RefreshTokenIssuance> IssueAsync(IUserCredential user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the raw refresh token and atomically rotates it: the old row is
    /// marked revoked with <c>ReplacedByTokenHash</c> pointing at the new one, and a
    /// fresh raw value is returned alongside the resolved credential.
    ///
    /// <para>
    /// Returns <c>null</c> for any failure (unknown, expired, revoked). On detected
    /// replay (presentation of an already-revoked-but-rotated token) the implementation
    /// MUST revoke the entire chain and bump the user's SessionVersion before
    /// returning <c>null</c>.
    /// </para>
    /// </summary>
    Task<RefreshTokenRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every active refresh token for the user. Used on logout and password change.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>Issue result — caller returns <see cref="RawToken"/> to the client and persists nothing else.</summary>
public sealed record RefreshTokenIssuance(string RawToken, DateTime ExpiresAt);

/// <summary>Rotation result — bundles the new raw token and the credential resolved from the old one.</summary>
public sealed record RefreshTokenRotation(string RawToken, DateTime ExpiresAt, IUserCredential Credential);
