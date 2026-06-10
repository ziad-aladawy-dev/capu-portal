using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

/// <summary>
/// Single-use password-reset token. As with <see cref="RefreshToken"/>, the raw
/// value is never persisted — only a SHA-256 hex digest — so a database read
/// alone cannot mint a usable reset link.
///
/// <para>
/// Lifecycle: <c>Active</c> while <see cref="ExpiresAt"/> is in the future and
/// <see cref="ConsumedAt"/> is null. Consuming the token (a successful reset) or
/// issuing a newer token for the same user retires it.
/// </para>
/// </summary>
public class PasswordResetToken : BaseEntity
{
    /// <summary>The user the token belongs to. Maps onto either <c>Staff.Id</c> or <c>Student.Id</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>Discriminator: <c>"Staff"</c> or <c>"Student"</c>.</summary>
    public string UserType { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the raw token. Unique — the lookup index.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC. After this point the token is invalid even if unconsumed.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC stamp set when the token is used or superseded. Null while active.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>True iff currently usable. Read-only derived state.</summary>
    public bool IsActive => ConsumedAt is null && DateTime.UtcNow < ExpiresAt;
}
