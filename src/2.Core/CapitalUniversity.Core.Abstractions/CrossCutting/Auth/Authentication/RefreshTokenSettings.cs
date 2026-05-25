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

    /// <summary>
    /// L11 — Upper bound on how far <c>RevokeChainAsync</c> walks the
    /// <c>ReplacedByTokenHash</c> rotation chain when forcibly revoking. The
    /// default (64) is well above any legitimate chain length (chains
    /// approximate <c>token-lifetime / rotation-interval</c>); the bound
    /// exists to defend against malformed data with cycles. Raise only if
    /// audit shows legitimate chains hitting the cap.
    /// </summary>
    [Range(1, 1024)]
    public int ChainRevokeDepth { get; set; } = 64;
}
