namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Server-side password complexity policy (spec 1.3): at least
/// <see cref="MinLength"/> characters with at least one uppercase letter, one
/// lowercase letter, one digit, and one special (non-alphanumeric) character.
///
/// Mirrors the client-side check in ChangePasswordModal / ResetPassword so the
/// rule is enforced even when the SPA is bypassed (H5 — previously change/reset
/// validated length only, letting a direct API caller set a trivial password).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Returns true when <paramref name="password"/> satisfies every rule.</summary>
    public static bool IsCompliant(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return false;

        bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSpecial = true; // any non-alphanumeric, matches /[^A-Za-z0-9]/
        }

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}
