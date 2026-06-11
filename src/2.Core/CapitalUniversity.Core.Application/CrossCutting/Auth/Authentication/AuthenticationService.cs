using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserCredentialResolver _credentialResolver;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IPermissionManagementService _permissionManagementService;
    private readonly ISessionVersionService _sessionVersionService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAuthAuditLogger? _audit;

    public AuthenticationService(
        IUserCredentialResolver credentialResolver,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IPermissionManagementService permissionManagementService,
        ISessionVersionService sessionVersionService,
        IRefreshTokenService refreshTokenService,
        IAuthAuditLogger? audit = null)
    {
        _credentialResolver = credentialResolver;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _permissionManagementService = permissionManagementService;
        _sessionVersionService = sessionVersionService;
        _refreshTokenService = refreshTokenService;
        _audit = audit;
    }

    public async Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            if (_audit is not null && request is not null)
                await _audit.LogAuthenticationFailedAsync(request.Identifier ?? string.Empty, "missing_credentials", cancellationToken);
            return null;
        }

        var credential = await _credentialResolver.ResolveCredentialAsync(request.Identifier, cancellationToken);

        // Always verify a "dummy" hash if user not found to prevent timing attacks
        string hashToVerify = credential?.PasswordHash ?? _passwordHasher.HashPassword("dummy_password_for_timing_safety");
        bool isPasswordValid = _passwordHasher.VerifyHashedPassword(hashToVerify, request.Password);

        if (credential == null || !isPasswordValid)
        {
            if (_audit is not null)
                await _audit.LogAuthenticationFailedAsync(request.Identifier, "invalid_credentials", cancellationToken);
            return null;
        }

        var passwordExpired = credential.PasswordExpiry.HasValue && DateTime.UtcNow > credential.PasswordExpiry;
        if (passwordExpired && _audit is not null)
        {
            // Still surfaced in the audit trail, but the login now succeeds and the
            // client is required to change the password before proceeding.
            await _audit.LogAuthenticationFailedAsync(request.Identifier, "password_expired", cancellationToken);
        }

        var token = _tokenService.GenerateToken(credential);
        var refresh = await _refreshTokenService.IssueAsync(credential, cancellationToken);

        var response = await _permissionManagementService.GetBootstrapContextAsync(credential, cancellationToken);
        response.Token = token;
        response.RefreshToken = refresh.RawToken;
        response.PasswordExpiryDate = credential.PasswordExpiry;
        response.RequiresPasswordChange = passwordExpired;

        return response;
    }

    public async Task<bool> LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _refreshTokenService.RevokeAllForUserAsync(userId, "logout", cancellationToken);
        var newVersion = await _sessionVersionService.IncrementVersionAsync(userId, cancellationToken);
        if (_audit is not null && newVersion.HasValue)
            await _audit.LogLogoutAsync(userId, cancellationToken);
        return newVersion.HasValue;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return false;
        }

        // H5 — Enforce password-complexity server-side (the SPA check is
        // bypassable). A non-compliant new password is rejected the same way
        // as any other invalid change request.
        if (!PasswordPolicy.IsCompliant(request.NewPassword))
        {
            return false;
        }

        var updated = await _credentialResolver.UpdatePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            _passwordHasher,
            cancellationToken);

        if (!updated) return false;

        // Bump session version + revoke refresh tokens so every credential issued
        // before this change stops working.
        await _refreshTokenService.RevokeAllForUserAsync(userId, "password-change", cancellationToken);
        await _sessionVersionService.IncrementVersionAsync(userId, cancellationToken);
        return true;
    }

    public async Task<LoginResponseDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var credential = await _credentialResolver.ResolveByIdAsync(userId, cancellationToken);
        if (credential is null) return null;
        var response = await _permissionManagementService.GetBootstrapContextAsync(credential, cancellationToken);
        response.PasswordExpiryDate = credential.PasswordExpiry;
        response.RequiresPasswordChange = credential.PasswordExpiry.HasValue && DateTime.UtcNow > credential.PasswordExpiry;
        return response;
    }

    public async Task<RefreshTokenResponseDto?> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken)) return null;

        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);
        if (rotation is null) return null;

        return new RefreshTokenResponseDto
        {
            Token = _tokenService.GenerateToken(rotation.Credential),
            RefreshToken = rotation.RawToken,
            PasswordExpiryDate = rotation.Credential.PasswordExpiry,
            RequiresPasswordChange = rotation.Credential.PasswordExpiry.HasValue
                && DateTime.UtcNow > rotation.Credential.PasswordExpiry
        };
    }
}
