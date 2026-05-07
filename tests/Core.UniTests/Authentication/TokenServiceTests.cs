using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;
using CapitalUniversity.Core.CrossCutting.Authentication;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_ValidUser_ReturnsTokenWithCorrectClaims()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Jwt:Secret"]).Returns("super_secret_key_which_should_be_long_enough_for_hmac_sha256_which_needs_to_be_32_bytes_at_least_to_work_with_hmac_sha256");
        mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("CapitalPortal");
        mockConfig.Setup(c => c["Jwt:Audience"]).Returns("CapitalPortal.Client");

        var credentialMock = new Mock<IUserCredential>();
        var id = Guid.NewGuid();
        credentialMock.Setup(c => c.Id).Returns(id);
        credentialMock.Setup(c => c.Identifier).Returns("admin123");
        credentialMock.Setup(c => c.Role).Returns("Admin");

        var tokenService = new TokenService(mockConfig.Object);

        // Act
        var tokenString = tokenService.GenerateToken(credentialMock.Object);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        Assert.NotNull(token);
        Assert.Equal("CapitalPortal", token.Issuer);

        var claims = token.Claims.ToList();
        Assert.Contains(claims, c => c.Type == "Id" && c.Value == id.ToString());
        Assert.Contains(claims, c => c.Type == "NationalId" && c.Value == "admin123");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Jti);
    }
}
