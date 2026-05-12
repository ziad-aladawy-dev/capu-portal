using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization;

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
