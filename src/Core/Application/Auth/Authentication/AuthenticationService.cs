using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;

namespace CapitalUniversity.Core.Application.Auth.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserCredentialResolver _credentialResolver;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthorizationResponseBuilder _authorizationResponseBuilder;
    private readonly ITokenService _tokenService;

    public AuthenticationService(
        IUserCredentialResolver credentialResolver,
        IPasswordHasher passwordHasher,
        IAuthorizationResponseBuilder authorizationResponseBuilder,
        ITokenService tokenService)
    {
        _credentialResolver = credentialResolver;
        _passwordHasher = passwordHasher;
        _authorizationResponseBuilder = authorizationResponseBuilder;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDto> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null; // Or throw ValidationException
        }

        var credential = await _credentialResolver.ResolveCredentialAsync(request.Identifier, cancellationToken);
        if (credential == null)
        {
            return null; // Or throw InvalidCredentialsException
        }

        if (DateTime.UtcNow > credential.PasswordExpiry)
        {
            return null; // Or throw PasswordExpiredException
        }

        var isPasswordValid = _passwordHasher.VerifyHashedPassword(credential.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            return null; // Or throw InvalidCredentialsException
        }

        var token = _tokenService.GenerateToken(credential);

        var authData = await _authorizationResponseBuilder.BuildAsync(credential, cancellationToken);

        var response = new LoginResponseDto
        {
            User = new UserInfoDto
            {
                Id = credential.Id,
                Name = credential.Name,
                Email = credential.Email,
                Attributes = new UserAttributesDto
                {
                    Uni = credential.UniAttribute,
                    Faculty = credential.FacultyAttribute,
                    Department = credential.DepartmentAttribute
                }
            },
            Token = token,
            AuthorizedScopes = authData.Scopes,
            Permissions = authData.Permissions,
            ActiveScope = authData.ActiveScope
        };

        return response;
    }
}
