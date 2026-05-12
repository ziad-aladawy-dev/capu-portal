using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IUserCredentialResolver
{
    Task<IUserCredential> ResolveCredentialAsync(string identifier, CancellationToken cancellationToken = default);
}
