using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.Auth.Authorization;

public interface IScopeResolver
{
    Task<AuthorizationScope> ResolveAsync(string domain, string year, string semester, CancellationToken cancellationToken = default);
}

public class AuthorizationScope
{
    public string Domain { get; set; }
    public string Year { get; set; }
    public string Semester { get; set; }
}
