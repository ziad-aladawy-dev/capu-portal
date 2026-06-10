using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

/// <summary>
/// Default <see cref="IPasswordResetSender"/>. No email/SMS infrastructure exists
/// yet, so the reset link is written to the application log. Replace this
/// registration with a real sender once SMTP (or an SMS gateway) is available —
/// the reset flow itself does not change.
/// </summary>
public class LoggingPasswordResetSender : IPasswordResetSender
{
    private readonly ILogger<LoggingPasswordResetSender> _logger;

    public LoggingPasswordResetSender(ILogger<LoggingPasswordResetSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string email, string name, string resetLink, CancellationToken cancellationToken = default)
    {
        // NOTE: link contains a single-use secret. This logging sender is for
        // development only; production must use a real delivery channel.
        _logger.LogWarning(
            "[PasswordReset] No email sender configured. Reset link for {Name} <{Email}>: {ResetLink}",
            name, email, resetLink);
        return Task.CompletedTask;
    }
}
