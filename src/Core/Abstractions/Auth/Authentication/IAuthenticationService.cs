using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.Auth.Authentication;

public interface IAuthenticationService
{
    // Single source of truth for validation, hashing, and token issuing
    Task<string> AuthenticateAsync(IUserCredential credential, string plainTextPassword, CancellationToken cancellationToken = default);
}