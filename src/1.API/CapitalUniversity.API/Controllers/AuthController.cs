using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IPasswordResetService _passwordResetService;

    public AuthController(IAuthenticationService authService, IPasswordResetService passwordResetService)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _authService.AuthenticateAsync(request, cancellationToken);
        if (response == null)
            return Unauthorized(new { Message = "Invalid credentials" });

        return Ok(response);
    }

    /// <summary>
    /// Revokes every active refresh token for the caller and bumps SessionVersion,
    /// invalidating every still-live access token in one move.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var ok = await _authService.LogoutAsync(userId, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Trades a valid refresh token for a fresh access + refresh pair. Anonymous on
    /// purpose — the access token may already have expired by the time the caller
    /// asks to refresh; the refresh token alone proves identity here.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshAsync(request, cancellationToken);
        if (response == null) return Unauthorized();
        return Ok(response);
    }

    /// <summary>
    /// Returns the profile, permissions, and active scope for the currently
    /// authenticated user. Used by the SPA on page reload to rehydrate auth
    /// state without requiring the user to log in again.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoginResponseDto>> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Changes the caller's password, revokes every refresh token, and bumps
    /// SessionVersion so every previously issued credential (including the one used
    /// to make this call) stops working.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var ok = await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return ok ? NoContent() : BadRequest(new { Message = "Password change failed." });
    }

    /// <summary>
    /// Requests a password-reset link for the given identifier. Always returns 200
    /// regardless of whether the account exists, so the endpoint cannot be used to
    /// enumerate valid identifiers.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _passwordResetService.RequestResetAsync(request.Identifier, cancellationToken);
        return Ok(new { Message = "If an account matches, a password reset link has been sent." });
    }

    /// <summary>
    /// Consumes a reset token and sets a new password. Revokes all existing sessions
    /// for the user on success.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var ok = await _passwordResetService.ResetAsync(request.Token, request.NewPassword, cancellationToken);
        return ok ? NoContent() : BadRequest(new { Message = "Invalid or expired reset token." });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
