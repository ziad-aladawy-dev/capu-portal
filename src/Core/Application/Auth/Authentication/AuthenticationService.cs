using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;

namespace CapitalUniversity.Core.Application.Auth.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthenticationService(IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public Task<string> AuthenticateAsync(IUserCredential credential, string plainTextPassword, CancellationToken cancellationToken = default)
    {
        if (credential == null)
            return Task.FromResult(string.Empty);

        if (DateTime.UtcNow > credential.PasswordExpiry)
            return Task.FromResult(string.Empty); // Or throw PasswordExpiredException

        var isPasswordValid = _passwordHasher.VerifyHashedPassword(credential.PasswordHash, plainTextPassword);

        if (!isPasswordValid)
            return Task.FromResult(string.Empty); // Or throw InvalidCredentialsException

        var token = _tokenService.GenerateToken(credential);
        return Task.FromResult(token);
    }
}
