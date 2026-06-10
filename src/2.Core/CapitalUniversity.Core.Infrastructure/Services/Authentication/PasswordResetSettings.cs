namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

public class PasswordResetSettings
{
    public const string SectionName = "Authentication:PasswordReset";

    /// <summary>How long a reset token stays valid, in minutes.</summary>
    public int ExpiryMinutes { get; set; } = 30;

    /// <summary>
    /// Frontend URL the reset link points at. The raw token is appended as
    /// <c>?token=...</c>. Override per environment.
    /// </summary>
    public string ResetUrlBase { get; set; } = "http://localhost:5173/reset-password";

    /// <summary>Minimum new-password length enforced server-side as a safety net.</summary>
    public int MinPasswordLength { get; set; } = 8;
}
