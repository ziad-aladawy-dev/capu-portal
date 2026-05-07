using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.Auth.Authorization;

public interface IScopeResolver
{
    Task<AuthorizationScope> ResolveAsync(string domain, string year, string semester, CancellationToken cancellationToken = default);
}

public class AuthorizationScope
{
    public required string Domain { get; set; }
    public required string Year { get; set; }
    public required string Semester { get; set; }
}
