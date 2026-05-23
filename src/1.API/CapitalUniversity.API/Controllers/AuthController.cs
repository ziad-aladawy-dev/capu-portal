using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IUserCredentialResolver _credentialResolver;
    private readonly IPermissionManagementService _permissionService;

    public AuthController(
        IAuthenticationService authService,
        IUserCredentialResolver credentialResolver,
        IPermissionManagementService permissionService)
    {
        _authService = authService;
        _credentialResolver = credentialResolver;
        _permissionService = permissionService;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoginResponseDto>> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var user = await _credentialResolver.ResolveByIdAsync(userId, cancellationToken);
        if (user == null)
            return Unauthorized();

        var response = await _permissionService.GetBootstrapContextAsync(user, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
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
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshAsync(request, cancellationToken);
        if (response == null) return Unauthorized();
        return Ok(response);
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

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
