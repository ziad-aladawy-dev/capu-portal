using System.ComponentModel.DataAnnotations;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Binds the <c>RefreshToken</c> configuration section. Sane defaults are baked in
/// so an absent section still yields a usable service.
/// </summary>
public class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    /// <summary>How long a freshly issued refresh token stays valid. Default: 30 days.</summary>
    [Range(1, 365)]
    public int ExpiryDays { get; set; } = 30;
}
