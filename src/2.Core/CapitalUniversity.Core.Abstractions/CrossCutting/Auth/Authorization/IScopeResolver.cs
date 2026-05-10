using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization;

public interface IScopeResolver
{
    Task<AuthorizationScope> ResolveAsync(string domain, string year, string semester, CancellationToken cancellationToken = default);
}
