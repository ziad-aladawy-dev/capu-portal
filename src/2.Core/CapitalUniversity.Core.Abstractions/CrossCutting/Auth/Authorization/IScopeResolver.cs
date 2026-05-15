using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public interface IScopeResolver
{
    Task<AuthorizationScope> ResolveAsync(Guid userId, string year, string semester, CancellationToken cancellationToken = default);
}
