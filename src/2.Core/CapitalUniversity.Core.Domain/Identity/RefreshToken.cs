using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

/// <summary>
/// Persistent refresh-token record. The raw token value is never stored — only a
/// SHA-256 hex digest of it, so a database compromise alone cannot resurrect a
/// usable refresh credential.
///
/// <para>
/// Lifecycle: <c>Active</c> while <see cref="ExpiresAt"/> is in the future and
/// <see cref="RevokedAt"/> is null. On rotation, the old row is revoked with
/// <see cref="ReplacedByTokenHash"/> pointing at the freshly issued successor,
/// forming a chain the service walks when it detects a replay (presentation of
/// an already-revoked-but-rotated token), at which point the entire chain plus
/// the user's SessionVersion are invalidated.
/// </para>
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>The user the token belongs to. Maps onto either <c>Staff.Id</c> or <c>Student.Id</c> (disjoint Guid spaces).</summary>
    public Guid UserId { get; set; }

    /// <summary>Discriminator: <c>"Staff"</c> or <c>"Student"</c>. Avoids a second lookup when resolving the credential.</summary>
    public string UserType { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the raw token. Unique — the lookup index.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC. After this point the token is invalid even if still active.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC stamp set when the row leaves <c>Active</c>. Null while active.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Hash of the successor token issued during rotation. Null on logout/password-change
    /// revocation, set only when this token was rotated through <c>RefreshAsync</c>.
    /// Presence-of-this + a later presentation of the same raw value = replay.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Free-form reason for revocation (e.g. <c>"rotated"</c>, <c>"logout"</c>, <c>"password-change"</c>, <c>"replay-detected"</c>, <c>"revoked-chain"</c>).</summary>
    public string? ReasonRevoked { get; set; }

    /// <summary>True iff currently usable. Read-only derived state.</summary>
    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
