using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateToken(IUserCredential user)
    {
        // M4 — Keep PII out of the JWT. The national id and email were previously
        // embedded here; a JWT is base64, trivially decoded in the browser, so any
        // script in the page context could read them. Neither was consumed
        // server-side (the only reader, CurrentUser.Email, had no consumers), so
        // they are dropped outright rather than relocated. The display Name is
        // retained: it is no government identifier and feeds the audit trail
        // (LogEntry.UserName).
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("Id", user.Id.ToString()), // Keep for compatibility if needed
            new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(SessionClaims.SessionVersion, user.SessionVersion.ToString())
        };

        if (user.StructureNodeId.HasValue)
        {
            claims.Add(new Claim("StructureNodeId", user.StructureNodeId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
