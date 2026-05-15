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

    public AuthenticationService(
        IUserCredentialResolver credentialResolver,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IPermissionManagementService permissionManagementService)
    {
        _credentialResolver = credentialResolver;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _permissionManagementService = permissionManagementService;
    }

    public async Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var credential = await _credentialResolver.ResolveCredentialAsync(request.Identifier, cancellationToken);
        
        // Always verify a "dummy" hash if user not found to prevent timing attacks
        string hashToVerify = credential?.PasswordHash ?? _passwordHasher.HashPassword("dummy_password_for_timing_safety");
        bool isPasswordValid = _passwordHasher.VerifyHashedPassword(hashToVerify, request.Password);

        if (credential == null || !isPasswordValid)
        {
            return null;
        }

        if (credential.PasswordExpiry.HasValue && DateTime.UtcNow > credential.PasswordExpiry)
        {
            return null;
        }

        var token = _tokenService.GenerateToken(credential);

        var response = await _permissionManagementService.GetBootstrapContextAsync(credential, cancellationToken);
        response.Token = token;

        return response;
    }
}
