using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization.DTOs;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authentication


;

public interface IAuthenticationService
{
    Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
