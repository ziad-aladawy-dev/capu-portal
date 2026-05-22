using System.Security.Cryptography;
using System.Text;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

public class RefreshTokenService : IRefreshTokenService
{
    private const int RawTokenBytes = 32;

    private readonly CoreDbContext _dbContext;
    private readonly IUserCredentialResolver _credentialResolver;
    private readonly ISessionVersionService _sessionVersionService;
    private readonly RefreshTokenSettings _settings;
    private readonly IAuthAuditLogger? _audit;

    public RefreshTokenService(
        CoreDbContext dbContext,
        IUserCredentialResolver credentialResolver,
        ISessionVersionService sessionVersionService,
        IOptions<RefreshTokenSettings> settings,
        IAuthAuditLogger? audit = null)
    {
        _dbContext = dbContext;
        _credentialResolver = credentialResolver;
        _sessionVersionService = sessionVersionService;
        _settings = settings.Value;
        _audit = audit;
    }

    public async Task<RefreshTokenIssuance> IssueAsync(IUserCredential user, CancellationToken cancellationToken = default)
    {
        var raw = GenerateRawToken();
        var hash = Hash(raw);
        var expiresAt = DateTime.UtcNow.AddDays(_settings.ExpiryDays);

        _dbContext.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            UserType = user.Role == "Student" ? "Student" : "Staff",
            TokenHash = hash,
            ExpiresAt = expiresAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshTokenIssuance(raw, expiresAt);
    }

    public async Task<RefreshTokenRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = Hash(rawToken);
        var existing = await _dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null) return null;

        // Replay: token was already revoked AND rotated through. Treat as compromise:
        // walk the rotation chain forward, revoke every successor, and bump the
        // user's SessionVersion so every still-live access token also stops working.
        if (existing.RevokedAt is not null)
        {
            if (existing.ReplacedByTokenHash is not null)
            {
                await RevokeChainAsync(existing, cancellationToken);
                await _sessionVersionService.IncrementVersionAsync(existing.UserId, cancellationToken);
                if (_audit is not null)
                    await _audit.LogRefreshReplayAsync(existing.UserId, cancellationToken);
            }
            return null;
        }

        if (DateTime.UtcNow >= existing.ExpiresAt)
        {
            return null;
        }

        var credential = await _credentialResolver.ResolveByIdAsync(existing.UserId, cancellationToken);
        if (credential is null) return null;

        // Issue the successor first so we can stamp the old row's ReplacedByTokenHash
        // in the same SaveChanges.
        var newRaw = GenerateRawToken();
        var newHash = Hash(newRaw);
        var newExpiresAt = DateTime.UtcNow.AddDays(_settings.ExpiryDays);

        _dbContext.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = existing.UserId,
            UserType = existing.UserType,
            TokenHash = newHash,
            ExpiresAt = newExpiresAt
        });

        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenHash = newHash;
        existing.ReasonRevoked = "rotated";

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshTokenRotation(newRaw, newExpiresAt, credential);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var active = await _dbContext.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (active.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var t in active)
        {
            t.RevokedAt = now;
            t.ReasonRevoked = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_audit is not null)
            await _audit.LogTokenRevokedAsync(userId, reason, active.Count, cancellationToken);
    }

    private async Task RevokeChainAsync(RefreshToken start, CancellationToken cancellationToken)
    {
        // Walk forward through ReplacedByTokenHash links revoking anything still active.
        // Loops should be impossible (each link points to a freshly issued token), but
        // cap depth to be safe against malformed data.
        var visited = new HashSet<string>();
        var current = start;
        var now = DateTime.UtcNow;
        var changed = false;

        for (int i = 0; i < 64; i++)
        {
            if (current.ReplacedByTokenHash is null) break;
            if (!visited.Add(current.ReplacedByTokenHash)) break;

            var next = await _dbContext.Set<RefreshToken>()
                .FirstOrDefaultAsync(t => t.TokenHash == current.ReplacedByTokenHash, cancellationToken);
            if (next is null) break;

            if (next.RevokedAt is null)
            {
                next.RevokedAt = now;
                next.ReasonRevoked = "revoked-chain";
                changed = true;
            }
            current = next;
        }

        if (changed) await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateRawToken()
    {
        // 256 bits of entropy — collision-resistant and well above brute-force range,
        // which is why a fast hash (SHA-256, not BCrypt/PBKDF2) is appropriate for
        // the at-rest digest.
        Span<byte> buf = stackalloc byte[RawTokenBytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string raw)
    {
        Span<byte> dest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), dest);
        var sb = new StringBuilder(64);
        foreach (var b in dest) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
