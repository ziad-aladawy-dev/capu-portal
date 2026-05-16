using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        IOptions<SessionVersionOptions> options)
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
            await Reject(context, "session.no_user_id");
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

            await Reject(context, "session.missing_version_claim");
            return;
        }

        if (!int.TryParse(tokenVersionClaim, out var tokenVersion))
        {
            await Reject(context, "session.invalid_version_claim");
            return;
        }

        var currentVersion = await sessionVersionService.GetCurrentVersionAsync(userId, context.RequestAborted);
        if (currentVersion is null)
        {
            await Reject(context, "session.user_not_found");
            return;
        }

        if (currentVersion.Value != tokenVersion)
        {
            await Reject(context, "session.revoked");
            return;
        }

        await _next(context);
    }

    private static Task Reject(HttpContext context, string reason)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = $"Bearer error=\"invalid_token\", error_description=\"{reason}\"";
        return Task.CompletedTask;
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
