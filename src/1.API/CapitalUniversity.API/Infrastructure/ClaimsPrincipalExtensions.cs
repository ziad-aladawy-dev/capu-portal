using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CapitalUniversity.API.Infrastructure;

/// <summary>
/// L7 — Shared user-id extractor. Tries the same three claim names that
/// <see cref="SessionVersionMiddleware"/> consults so a token issued with any
/// of them yields the same id everywhere in the codebase. Falls back to
/// <see cref="Guid.Empty"/> + <c>false</c> when no claim parses, so callers
/// can `return Unauthorized()` consistently.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        if (principal is null) return false;

        var raw = principal.FindFirstValue("Id")
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(raw, out userId);
    }
}
