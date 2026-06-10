namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class RefreshTokenRequestDto
{
    /// <summary>
    /// Raw refresh token issued by a prior /auth/login or /auth/refresh call.
    /// The server hashes this before lookup; the raw value is never persisted.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>When the user's password expires (UTC), or null if not applicable.</summary>
    public DateTime? PasswordExpiryDate { get; set; }

    /// <summary>True when the password has expired and a change is required.</summary>
    public bool RequiresPasswordChange { get; set; }
}

public class ForgotPasswordRequestDto
{
    /// <summary>The account identifier (National ID) requesting a reset.</summary>
    public string Identifier { get; set; } = string.Empty;
}

public class ResetPasswordRequestDto
{
    /// <summary>Raw reset token from the emailed/issued reset link.</summary>
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
