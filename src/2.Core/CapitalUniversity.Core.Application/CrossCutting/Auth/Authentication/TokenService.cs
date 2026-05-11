using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(IUserCredential user)
    {
        var claims = new[]
        {
            new Claim("Id", user.Id.ToString()),
            new Claim("NationalId", user.Identifier),
            new Claim(ClaimTypes.Role, user.Role ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var secretKey = _configuration["Jwt:Secret"] ?? "super_secret_key_which_should_be_long_enough_for_hmac_sha256_which_needs_to_be_32_bytes_at_least_to_work_with_hmac_sha256";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "CapitalPortal",
            audience: _configuration["Jwt:Audience"] ?? "CapitalPortal.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
