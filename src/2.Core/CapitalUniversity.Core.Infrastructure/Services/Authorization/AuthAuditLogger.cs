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

    // Metadata dictionary keys. Constants so a typo in one call site fails
    // alongside every other read of the same key, and the audit-log schema
    // stays consistent across event types.
    internal const string MetaEventType = "EventType";
    internal const string MetaUserId = "UserId";
    internal const string MetaReason = "Reason";
    internal const string MetaPath = "Path";
    internal const string MetaRequiredPermission = "RequiredPermission";
    internal const string MetaIdentifierHash = "IdentifierHash";
    internal const string MetaTokenCount = "TokenCount";
    internal const string MetaTargetUserId = "TargetUserId";
    internal const string MetaRolesAdded = "RolesAdded";
    internal const string MetaRolesRemoved = "RolesRemoved";

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
                [MetaEventType] = AuthAuditEventTypes.PermissionDenied,
                [MetaUserId] = userId,
                [MetaRequiredPermission] = requiredPermission,
                [MetaPath] = path ?? _httpContextAccessor.HttpContext?.Request?.Path.Value ?? string.Empty,
            }));

    public Task LogAuthenticationFailedAsync(string identifier, string reason, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogWarningAsync(
            "Authentication failed.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                [MetaEventType] = AuthAuditEventTypes.AuthFailed,
                // Hash the identifier so we can correlate brute-force attempts
                // without retaining the raw value.
                [MetaIdentifierHash] = HashIdentifier(identifier),
                [MetaReason] = reason,
            }));

    public Task LogLogoutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogInfoAsync(
            "User logged out.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                [MetaEventType] = AuthAuditEventTypes.Logout,
                [MetaUserId] = userId,
            }));

    public Task LogTokenRevokedAsync(Guid userId, string reason, int tokenCount, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogInfoAsync(
            "Refresh tokens revoked.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                [MetaEventType] = AuthAuditEventTypes.TokenRevoked,
                [MetaUserId] = userId,
                [MetaReason] = reason,
                [MetaTokenCount] = tokenCount,
            }));

    public Task LogRefreshReplayAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogWarningAsync(
            "Refresh-token replay detected — rotation chain revoked.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                [MetaEventType] = AuthAuditEventTypes.RefreshReplay,
                [MetaUserId] = userId,
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
                [MetaEventType] = AuthAuditEventTypes.RoleAssignmentChanged,
                [MetaTargetUserId] = targetUserId,
                [MetaRolesAdded] = rolesAdded.ToArray(),
                [MetaRolesRemoved] = rolesRemoved.ToArray(),
            }));

    public Task LogSessionRejectedAsync(Guid userId, string reason, string? path = null, CancellationToken cancellationToken = default) =>
        SafeLogAsync(_appLogger.LogWarningAsync(
            "Session-version check rejected token.",
            Source,
            _httpContextAccessor.HttpContext,
            new Dictionary<string, object>
            {
                [MetaEventType] = AuthAuditEventTypes.SessionRejected,
                [MetaUserId] = userId,
                [MetaReason] = reason,
                [MetaPath] = path ?? _httpContextAccessor.HttpContext?.Request?.Path.Value ?? string.Empty,
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
    public const string SessionRejected = "session_rejected";
}
