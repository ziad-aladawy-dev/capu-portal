using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Delivers a password-reset link to the user. The default implementation logs the
/// link (no email infrastructure exists yet); a real email/SMS sender can be swapped
/// in via DI without touching the reset flow.
/// </summary>
public interface IPasswordResetSender
{
    Task SendAsync(string email, string name, string resetLink, CancellationToken cancellationToken = default);
}
