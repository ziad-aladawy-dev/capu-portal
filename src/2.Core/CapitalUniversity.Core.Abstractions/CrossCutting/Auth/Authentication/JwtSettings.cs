using System.ComponentModel.DataAnnotations;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    // H12 — HS256 derives a 256-bit MAC; using a key shorter than 32 bytes
    // (256 bits) makes the signing key the bottleneck against brute-force.
    // The lower bound is enforced at bind-time via ValidateDataAnnotations()
    // on AddOptions<JwtSettings>(), so a misconfigured deployment fails fast
    // at startup instead of silently shipping a weak token signer.
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters (HS256 requires ≥256-bit key material).")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 10080)] // 1 minute to 1 week
    public int ExpiryMinutes { get; set; }
}
