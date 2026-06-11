using System.Security.Cryptography;
using System.Text;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

public class PasswordResetService : IPasswordResetService
{
    private const int RawTokenBytes = 32;

    private readonly CoreDbContext _dbContext;
    private readonly IUserCredentialResolver _credentialResolver;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionVersionService _sessionVersionService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordResetSender _sender;
    private readonly PasswordResetSettings _settings;

    public PasswordResetService(
        CoreDbContext dbContext,
        IUserCredentialResolver credentialResolver,
        IPasswordHasher passwordHasher,
        ISessionVersionService sessionVersionService,
        IRefreshTokenService refreshTokenService,
        IPasswordResetSender sender,
        IOptions<PasswordResetSettings> settings)
    {
        _dbContext = dbContext;
        _credentialResolver = credentialResolver;
        _passwordHasher = passwordHasher;
        _sessionVersionService = sessionVersionService;
        _refreshTokenService = refreshTokenService;
        _sender = sender;
        _settings = settings.Value;
    }

    public async Task RequestResetAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return;

        var credential = await _credentialResolver.ResolveCredentialAsync(identifier, cancellationToken);
        // No account → return silently so callers cannot probe which identifiers exist.
        if (credential is null) return;

        // Supersede any still-active tokens for this user, so only the newest link works.
        var active = await _dbContext.Set<PasswordResetToken>()
            .Where(t => t.UserId == credential.Id && t.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var t in active) t.ConsumedAt = now;

        var raw = GenerateRawToken();
        _dbContext.Set<PasswordResetToken>().Add(new PasswordResetToken
        {
            UserId = credential.Id,
            UserType = credential.Role == "Student" ? "Student" : "Staff",
            TokenHash = Hash(raw),
            ExpiresAt = now.AddMinutes(Math.Max(1, _settings.ExpiryMinutes))
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var separator = _settings.ResetUrlBase.Contains('?') ? '&' : '?';
        var resetLink = $"{_settings.ResetUrlBase}{separator}token={Uri.EscapeDataString(raw)}";
        await _sender.SendAsync(credential.Email, credential.Name, resetLink, cancellationToken);
    }

    public async Task<bool> ResetAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(newPassword)) return false;
        // H5 — Enforce the full complexity policy server-side, not just length.
        if (!CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.PasswordPolicy.IsCompliant(newPassword)) return false;

        var hash = Hash(rawToken);
        var token = await _dbContext.Set<PasswordResetToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || token.ConsumedAt is not null || DateTime.UtcNow >= token.ExpiresAt)
            return false;

        var updated = await _credentialResolver.SetPasswordAsync(token.UserId, newPassword, _passwordHasher, cancellationToken);
        if (!updated) return false;

        token.ConsumedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate every existing session so a reset (which may follow an account
        // takeover) forces a fresh login everywhere.
        await _refreshTokenService.RevokeAllForUserAsync(token.UserId, "password-reset", cancellationToken);
        await _sessionVersionService.IncrementVersionAsync(token.UserId, cancellationToken);
        return true;
    }

    private static string GenerateRawToken()
    {
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
