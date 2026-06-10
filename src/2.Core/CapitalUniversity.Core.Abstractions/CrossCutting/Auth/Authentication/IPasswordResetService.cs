using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Orchestrates the forgot-password / reset-password flow. The portal owns the
/// reset token; delivery of the reset link is delegated to <see cref="IPasswordResetSender"/>.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Issues a single-use reset token for the account matching <paramref name="identifier"/>
    /// and dispatches a reset link through the configured sender. Intentionally does
    /// nothing observable when no account matches, so callers cannot enumerate users.
    /// </summary>
    Task RequestResetAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a reset token and sets the new password. Returns false when the token
    /// is unknown, expired, already used, or the new password fails the minimum policy.
    /// On success, all existing sessions for the user are revoked.
    /// </summary>
    Task<bool> ResetAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default);
}
