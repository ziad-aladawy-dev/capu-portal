using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Application.Auth.Authentication;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_ValidAdminCredentials_ReturnsResponse()
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockAuthBuilder = new Mock<IAuthorizationResponseBuilder>();
        var mockTokenService = new Mock<ITokenService>();

        var credentialMock = new Mock<IUserCredential>();
        credentialMock.Setup(c => c.Id).Returns(Guid.NewGuid());
        credentialMock.Setup(c => c.Identifier).Returns("admin123");
        credentialMock.Setup(c => c.PasswordHash).Returns("hashed");
        credentialMock.Setup(c => c.PasswordExpiry).Returns(DateTime.UtcNow.AddDays(1));
        credentialMock.Setup(c => c.Role).Returns("Admin");
        credentialMock.Setup(c => c.Name).Returns("Admin User");
        credentialMock.Setup(c => c.Email).Returns("admin@uni.edu");
        credentialMock.Setup(c => c.UniAttribute).Returns("Uni");
        credentialMock.Setup(c => c.FacultyAttribute).Returns("IT");
        credentialMock.Setup(c => c.DepartmentAttribute).Returns("CS");

        var request = new LoginRequestDto { Identifier = "admin123", Password = "password" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialMock.Object);

        mockHasher.Setup(h => h.VerifyHashedPassword("hashed", "password"))
            .Returns(true);

        mockTokenService.Setup(t => t.GenerateToken(credentialMock.Object))
            .Returns("token123");

        var authScopes = new AuthorizedScopesDto();
        var permissions = new List<PermissionDto>();
        var activeScope = new ActiveScopeDto();

        mockAuthBuilder.Setup(a => a.BuildAsync(credentialMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync((authScopes, permissions, activeScope));

        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockAuthBuilder.Object, mockTokenService.Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("token123", result.Token);
        Assert.Equal("admin123", request.Identifier);
        Assert.Equal("Admin", credentialMock.Object.Role);
        Assert.Equal(authScopes, result.AuthorizedScopes);
        Assert.Equal(permissions, result.Permissions);
        Assert.Equal(activeScope, result.ActiveScope);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidStudentCredentials_ReturnsResponse()
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockAuthBuilder = new Mock<IAuthorizationResponseBuilder>();
        var mockTokenService = new Mock<ITokenService>();

        var credentialMock = new Mock<IUserCredential>();
        credentialMock.Setup(c => c.Id).Returns(Guid.NewGuid());
        credentialMock.Setup(c => c.Identifier).Returns("student123");
        credentialMock.Setup(c => c.PasswordHash).Returns("hashed");
        credentialMock.Setup(c => c.PasswordExpiry).Returns(DateTime.UtcNow.AddDays(1));
        credentialMock.Setup(c => c.Role).Returns("Student");
        credentialMock.Setup(c => c.Name).Returns("Student User");
        credentialMock.Setup(c => c.Email).Returns("student@uni.edu");
        credentialMock.Setup(c => c.UniAttribute).Returns("Uni");
        credentialMock.Setup(c => c.FacultyAttribute).Returns("Engineering");
        credentialMock.Setup(c => c.DepartmentAttribute).Returns("Civil");

        var request = new LoginRequestDto { Identifier = "student123", Password = "password" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialMock.Object);

        mockHasher.Setup(h => h.VerifyHashedPassword("hashed", "password"))
            .Returns(true);

        mockTokenService.Setup(t => t.GenerateToken(credentialMock.Object))
            .Returns("token123");

        var authScopes = new AuthorizedScopesDto();
        var permissions = new List<PermissionDto>();
        var activeScope = new ActiveScopeDto();

        mockAuthBuilder.Setup(a => a.BuildAsync(credentialMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync((authScopes, permissions, activeScope));

        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockAuthBuilder.Object, mockTokenService.Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("token123", result.Token);
        Assert.Equal("student123", request.Identifier);
        Assert.Equal("Student", credentialMock.Object.Role);
        Assert.Equal(authScopes, result.AuthorizedScopes);
        Assert.Equal(permissions, result.Permissions);
        Assert.Equal(activeScope, result.ActiveScope);
    }
}
