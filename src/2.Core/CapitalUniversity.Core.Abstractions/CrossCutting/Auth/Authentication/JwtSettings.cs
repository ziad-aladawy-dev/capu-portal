using System.ComponentModel.DataAnnotations;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 10080)] // 1 minute to 1 week
    public int ExpiryMinutes { get; set; }
}
