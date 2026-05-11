using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IAuthorizationResponseBuilder
{
    Task<(AuthorizedScopesDto Scopes, System.Collections.Generic.List<PermissionDto> Permissions, ActiveScopeDto ActiveScope)> BuildAsync(IUserCredential user, CancellationToken cancellationToken = default);
}
