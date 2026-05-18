using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

/// <summary>
/// <see cref="IAuthAuditLogger"/> implementation that routes every security
/// event through <see cref="IAppLogger"/>. The async pipeline (Channel-backed
/// queue + flush worker) keeps the auth hot path free of synchronous log I/O.
///
/// <para>
/// Identifier values (emails / usernames) are passed through <see cref="LogScrubber"/>
/// so PII (notably login identifiers) is not retained in the audit trail.
/// </para>
/// </summary>
public class AuthAuditLogger : IAuthAuditLogger
{
    internal const string Source = "AuthAudit";

    private readonly IAppLogger _appLogger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthAuditLogger(IAppLogger appLogger, IHttpContextAccessor httpContextAccessor)
    {
        _appLogger = appLogger;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogPermissionDeniedAsync(Guid userId, string requiredPermission, string? path = null, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogWarningAsync(
            "Permission denied.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                ["EventType"] = AuthAuditEventTypes.PermissionDenied,
                ["UserId"] = userId,
                ["RequiredPermission"] = requiredPermission,
                ["Path"] = path ?? _httpContextAccessor.HttpContext?.Request?.Path.Value ?? string.Empty,
            }));

    public Task LogAuthenticationFailedAsync(string identifier, string reason, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogWarningAsync(
            "Authentication failed.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                ["EventType"] = AuthAuditEventTypes.AuthFailed,
                // Hash the identifier so we can correlate brute-force attempts
                // without retaining the raw value.
                ["IdentifierHash"] = HashIdentifier(identifier),
                ["Reason"] = reason,
            }));

    public Task LogLogoutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogInfoAsync(
            "User logged out.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                ["EventType"] = AuthAuditEventTypes.Logout,
                ["UserId"] = userId,
            }));

    public Task LogTokenRevokedAsync(Guid userId, string reason, int tokenCount, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogInfoAsync(
            "Refresh tokens revoked.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                ["EventType"] = AuthAuditEventTypes.TokenRevoked,
                ["UserId"] = userId,
                ["Reason"] = reason,
                ["TokenCount"] = tokenCount,
            }));

    public Task LogRefreshReplayAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogWarningAsync(
            "Refresh-token replay detected — rotation chain revoked.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                ["EventType"] = AuthAuditEventTypes.RefreshReplay,
                ["UserId"] = userId,
            }));

    public Task LogRoleAssignmentChangedAsync(
        Guid targetUserId,
        IReadOnlyCollection<Guid> rolesAdded,
        IReadOnlyCollection<Guid> rolesRemoved,
        CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogInfoAsync(
            "Role assignment changed.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                ["EventType"] = AuthAuditEventTypes.RoleAssignmentChanged,
                ["TargetUserId"] = targetUserId,
                ["RolesAdded"] = rolesAdded.ToArray(),
                ["RolesRemoved"] = rolesRemoved.ToArray(),
            }));

    private static async Task SafeLogAsync(Task pending)
    {
        try
        {
            await pending;
        }
        catch
        {
            // The audit pipeline must never bubble back into the auth path —
            // a logging glitch should not deny a legitimate caller.
        }
    }

    private static string HashIdentifier(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return string.Empty;
        Span<byte> dest = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identifier), dest);
        var sb = new System.Text.StringBuilder(64);
        foreach (var b in dest) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

public static class AuthAuditEventTypes
{
    public const string PermissionDenied = "permission_denied";
    public const string AuthFailed = "auth_failed";
    public const string Logout = "logout";
    public const string TokenRevoked = "token_revoked";
    public const string RefreshReplay = "refresh_replay";
    public const string RoleAssignmentChanged = "role_assignment_changed";
}
