using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthController(IAuthenticationService authService)
    {
        _authService = authService;
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
    /// Invalidates every token outstanding for the caller by bumping their
    /// <c>SessionVersion</c>. Subsequent requests carrying the now-stale token
    /// fail the session-version middleware check.
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
    /// Issues a fresh JWT for the already-authenticated caller. The session-version
    /// middleware ran on the inbound request, so reaching this method already proves
    /// the caller's existing token is still valid.
    /// </summary>
    [Authorize]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResponseDto>> Refresh(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await _authService.RefreshAsync(userId, cancellationToken);
        if (response == null) return Unauthorized();
        return Ok(response);
    }

    /// <summary>
    /// Changes the caller's password and bumps SessionVersion so every previously
    /// issued token (including the one used to make this call) stops working.
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
