using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;

namespace CapitalUniversity.Core.CrossCutting.Security;

public class ScopeResolver : IScopeResolver
{
    public Task<AuthorizationScope> ResolveAsync(string domain, string year, string semester, CancellationToken cancellationToken = default)
    {
        // Simple passthrough for now, can be extended to fetch from headers/context if parameters are missing
        return Task.FromResult(new AuthorizationScope
        {
            Domain = domain,
            Year = year,
            Semester = semester
        });
    }
}
