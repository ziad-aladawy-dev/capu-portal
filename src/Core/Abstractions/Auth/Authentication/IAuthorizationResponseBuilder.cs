using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;

namespace CapitalUniversity.Core.Abstractions.Auth.Authentication;

public interface IAuthorizationResponseBuilder
{
    Task<(AuthorizedScopesDto Scopes, System.Collections.Generic.List<PermissionDto> Permissions, ActiveScopeDto ActiveScope)> BuildAsync(IUserCredential user, CancellationToken cancellationToken = default);
}
