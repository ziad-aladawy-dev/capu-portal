using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IAuthenticationService
{
    Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every active refresh token for the user and bumps SessionVersion so any
    /// still-live access tokens fail the middleware check on next use.
    /// </summary>
    Task<bool> LogoutAsync(System.Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies <paramref name="request"/>.CurrentPassword, sets the new hash, revokes
    /// all refresh tokens, bumps SessionVersion so old access tokens stop working.
    /// </summary>
    Task<bool> ChangePasswordAsync(System.Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user profile, permissions, and active scope for the currently
    /// authenticated user identified by <paramref name="userId"/>. Returns null if
    /// the user no longer exists. Does NOT issue new tokens — this is a read-only
    /// bootstrap used to rehydrate SPA auth state on page reload.
    /// </summary>
    Task<LoginResponseDto?> GetCurrentUserAsync(System.Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the supplied refresh token, rotates it, and returns a fresh access +
    /// refresh pair. The previous refresh token becomes invalid. On any failure
    /// (unknown / expired / revoked) returns null; replay attempts trigger a full chain
    /// revocation and a SessionVersion bump inside the implementation.
    /// </summary>
    Task<RefreshTokenResponseDto?> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
}
