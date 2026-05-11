using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IAuthenticationService
{
    Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
