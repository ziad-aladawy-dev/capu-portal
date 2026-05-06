using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CapitalUniversity.Core.CrossCutting.Authentication;

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
            new Claim("NationalId", user.Identifier),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            // Role or other module-specific properties should be added to IUserCredential if needed
        };

        var secretKey = _configuration["Jwt:Secret"] ?? "super_secret_key_which_should_be_long_enough_for_hmac_sha256";
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
