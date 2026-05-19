using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;
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
        var mockPermService = new Mock<IPermissionManagementService>();
        var mockTokenService = new Mock<ITokenService>();

        var credentialMock = new Mock<IUserCredential>();
        credentialMock.Setup(c => c.Id).Returns(Guid.NewGuid());
        credentialMock.Setup(c => c.Identifier).Returns("admin123");
        credentialMock.Setup(c => c.PasswordHash).Returns("hashed");
        credentialMock.Setup(c => c.PasswordExpiry).Returns(DateTime.UtcNow.AddDays(1));
        credentialMock.Setup(c => c.Role).Returns("Admin");
        credentialMock.Setup(c => c.Name).Returns("Admin User");
        credentialMock.Setup(c => c.Email).Returns("admin@uni.edu");

        var request = new LoginRequestDto { Identifier = "admin123", Password = "password" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialMock.Object);

        mockHasher.Setup(h => h.VerifyHashedPassword("hashed", "password"))
            .Returns(true);

        mockTokenService.Setup(t => t.GenerateToken(credentialMock.Object))
            .Returns("token123");

        var loginResponse = new LoginResponseDto
        {
            Permissions = new List<PermissionDto>(),
            ActiveScope = new ActiveScopeDto()
        };

        mockPermService.Setup(a => a.GetBootstrapContextAsync(credentialMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);

        var mockRefresh = new Mock<IRefreshTokenService>();
        mockRefresh.Setup(r => r.IssueAsync(It.IsAny<IUserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenIssuance("rt-1", DateTime.UtcNow.AddDays(30)));
        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockTokenService.Object, mockPermService.Object, new Mock<ISessionVersionService>().Object, mockRefresh.Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("token123", result.Token);
        Assert.Equal("rt-1", result.RefreshToken);
        Assert.Equal(loginResponse.Permissions, result.Permissions);
        Assert.Equal(loginResponse.ActiveScope, result.ActiveScope);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidStudentCredentials_ReturnsResponse()
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockPermService = new Mock<IPermissionManagementService>();
        var mockTokenService = new Mock<ITokenService>();

        var credentialMock = new Mock<IUserCredential>();
        credentialMock.Setup(c => c.Id).Returns(Guid.NewGuid());
        credentialMock.Setup(c => c.Identifier).Returns("student123");
        credentialMock.Setup(c => c.PasswordHash).Returns("hashed");
        credentialMock.Setup(c => c.PasswordExpiry).Returns(DateTime.UtcNow.AddDays(1));
        credentialMock.Setup(c => c.Role).Returns("Student");
        credentialMock.Setup(c => c.Name).Returns("Student User");
        credentialMock.Setup(c => c.Email).Returns("student@uni.edu");

        var request = new LoginRequestDto { Identifier = "student123", Password = "password" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialMock.Object);

        mockHasher.Setup(h => h.VerifyHashedPassword("hashed", "password"))
            .Returns(true);

        mockTokenService.Setup(t => t.GenerateToken(credentialMock.Object))
            .Returns("token123");

        var loginResponse = new LoginResponseDto
        {
            Permissions = new List<PermissionDto>(),
            ActiveScope = new ActiveScopeDto()
        };

        mockPermService.Setup(a => a.GetBootstrapContextAsync(credentialMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);

        var mockRefresh = new Mock<IRefreshTokenService>();
        mockRefresh.Setup(r => r.IssueAsync(It.IsAny<IUserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenIssuance("rt-1", DateTime.UtcNow.AddDays(30)));
        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockTokenService.Object, mockPermService.Object, new Mock<ISessionVersionService>().Object, mockRefresh.Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("token123", result.Token);
        Assert.Equal("rt-1", result.RefreshToken);
        Assert.Equal(loginResponse.Permissions, result.Permissions);
        Assert.Equal(loginResponse.ActiveScope, result.ActiveScope);
    }

    [Theory]
    [InlineData(null, "password")]
    [InlineData("", "password")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    [InlineData(null, null)]
    public async Task AuthenticateAsync_InvalidRequest_ReturnsNull(string? identifier, string? password)
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockPermService = new Mock<IPermissionManagementService>();
        var mockTokenService = new Mock<ITokenService>();

        var request = new LoginRequestDto { Identifier = identifier!, Password = password! };
        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockTokenService.Object, mockPermService.Object, new Mock<ISessionVersionService>().Object, new Mock<IRefreshTokenService>().Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockPermService = new Mock<IPermissionManagementService>();
        var mockTokenService = new Mock<ITokenService>();

        var request = new LoginRequestDto { Identifier = "nonexistent", Password = "password" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUserCredential?)null);

        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockTokenService.Object, mockPermService.Object, new Mock<ISessionVersionService>().Object, new Mock<IRefreshTokenService>().Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_ExpiredPassword_ReturnsNull()
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockPermService = new Mock<IPermissionManagementService>();
        var mockTokenService = new Mock<ITokenService>();

        var credentialMock = new Mock<IUserCredential>();
        credentialMock.Setup(c => c.PasswordExpiry).Returns(DateTime.UtcNow.AddMinutes(-1));

        var request = new LoginRequestDto { Identifier = "user", Password = "password" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialMock.Object);

        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockTokenService.Object, mockPermService.Object, new Mock<ISessionVersionService>().Object, new Mock<IRefreshTokenService>().Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        var mockResolver = new Mock<IUserCredentialResolver>();
        var mockHasher = new Mock<IPasswordHasher>();
        var mockPermService = new Mock<IPermissionManagementService>();
        var mockTokenService = new Mock<ITokenService>();

        var credentialMock = new Mock<IUserCredential>();
        credentialMock.Setup(c => c.PasswordHash).Returns("hashed");
        credentialMock.Setup(c => c.PasswordExpiry).Returns(DateTime.UtcNow.AddDays(1));

        var request = new LoginRequestDto { Identifier = "user", Password = "wrongpassword" };

        mockResolver.Setup(r => r.ResolveCredentialAsync(request.Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialMock.Object);

        mockHasher.Setup(h => h.VerifyHashedPassword("hashed", "wrongpassword"))
            .Returns(false);

        var authService = new AuthenticationService(mockResolver.Object, mockHasher.Object, mockTokenService.Object, mockPermService.Object, new Mock<ISessionVersionService>().Object, new Mock<IRefreshTokenService>().Object);

        // Act
        var result = await authService.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }
}

