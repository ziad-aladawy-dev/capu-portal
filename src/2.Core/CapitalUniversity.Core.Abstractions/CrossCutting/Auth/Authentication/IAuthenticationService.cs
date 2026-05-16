using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IAuthenticationService
{
    Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bumps the user's SessionVersion. Every outstanding token (including any reused
    /// after this call) fails the middleware check.
    /// </summary>
    Task<bool> LogoutAsync(System.Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies <paramref name="request"/>.CurrentPassword, sets the new hash, bumps
    /// SessionVersion (so old tokens stop working), returns true on success.
    /// </summary>
    Task<bool> ChangePasswordAsync(System.Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a fresh JWT for the already-authenticated caller. The middleware has
    /// already validated SessionVersion on the inbound request, so the only work here
    /// is to mint a new token carrying the current version.
    /// </summary>
    Task<RefreshTokenResponseDto?> RefreshAsync(System.Guid userId, CancellationToken cancellationToken = default);
}
