using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.API.Infrastructure;

/// <summary>
/// Runs after JWT authentication. For any authenticated request, compares the token's
/// <c>session_version</c> claim against the user's current row value. Mismatch → 401.
///
/// A configurable grace window (<see cref="SessionVersionOptions.GraceEndUtc"/>) lets
/// tokens issued before the SessionVersion deploy continue to work, but only while
/// the grace period is open. After that, tokens missing the claim are rejected.
/// </summary>
public class SessionVersionMiddleware
{
    private readonly RequestDelegate _next;

    public SessionVersionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISessionVersionService sessionVersionService,
        IOptions<SessionVersionOptions> options,
        IAuthAuditLogger? audit = null)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirstValue("Id")
                          ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            await Reject(context, "session.no_user_id", audit, Guid.Empty);
            return;
        }

        var tokenVersionClaim = context.User.FindFirstValue(SessionClaims.SessionVersion);

        if (string.IsNullOrEmpty(tokenVersionClaim))
        {
            // Pre-deploy token. Allow only inside the grace window — otherwise reject.
            if (options.Value.GraceEndUtc is { } graceEnd && DateTime.UtcNow < graceEnd)
            {
                await _next(context);
                return;
            }

            await Reject(context, "session.missing_version_claim", audit, userId);
            return;
        }

        if (!int.TryParse(tokenVersionClaim, out var tokenVersion))
        {
            await Reject(context, "session.invalid_version_claim", audit, userId);
            return;
        }

        var currentVersion = await sessionVersionService.GetCurrentVersionAsync(userId, context.RequestAborted);
        if (currentVersion is null)
        {
            await Reject(context, "session.user_not_found", audit, userId);
            return;
        }

        if (currentVersion.Value != tokenVersion)
        {
            await Reject(context, "session.revoked", audit, userId);
            return;
        }

        await _next(context);
    }

    private static async Task Reject(HttpContext context, string reason, IAuthAuditLogger? audit, Guid userId)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = $"Bearer error=\"invalid_token\", error_description=\"{reason}\"";

        // Stable machine-readable code in the body so the SPA can distinguish a
        // revoked/forced-logout session from an ordinary expired token and show
        // the right message. A version mismatch (logout elsewhere, password
        // change, replayed refresh token) maps to "session_revoked".
        var code = reason == "session.revoked" ? "session_revoked" : reason.Replace('.', '_');
        await context.Response.WriteAsJsonAsync(new { reason = code });

        // M16 — fire-and-forget audit so a session rejection (forced logout,
        // stale token after password change, etc.) is visible in the audit
        // trail. Audit failures are swallowed inside the logger so they can
        // never deny the rejection itself.
        if (audit is not null)
        {
            await audit.LogSessionRejectedAsync(userId, reason, context.Request.Path.Value);
        }
    }
}

public class SessionVersionOptions
{
    public const string SectionName = "Authentication:Session";

    /// <summary>
    /// Until this UTC instant, tokens that lack a <c>session_version</c> claim
    /// continue to be accepted (covers users mid-session at deploy time). Leave
    /// null to require the claim on every token.
    /// </summary>
    public DateTime? GraceEndUtc { get; set; }
}
