namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;

/// <summary>
/// Records authorization-relevant security events: login failures, permission
/// denials, token revocations, role/permission edits. Infrastructure-only —
/// the contract sits in Abstractions so the call sites (PermissionHandler,
/// AuthenticationService, RefreshTokenService, PermissionManagementService)
/// can depend on it without taking a hard reference on the logging
/// implementation.
///
/// <para>
/// All methods are fire-and-forget from the caller's perspective: the
/// underlying <c>IAppLogger</c> stages onto the async audit pipeline, so a
/// slow log sink never blocks the auth hot path. Implementations must catch
/// internal failures rather than throwing back into the caller.
/// </para>
/// </summary>
public interface IAuthAuditLogger
{
    System.Threading.Tasks.Task LogPermissionDeniedAsync(
        System.Guid userId,
        string requiredPermission,
        string? path = null,
        System.Threading.CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task LogAuthenticationFailedAsync(
        string identifier,
        string reason,
        System.Threading.CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task LogLogoutAsync(
        System.Guid userId,
        System.Threading.CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task LogTokenRevokedAsync(
        System.Guid userId,
        string reason,
        int tokenCount,
        System.Threading.CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task LogRefreshReplayAsync(
        System.Guid userId,
        System.Threading.CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task LogRoleAssignmentChangedAsync(
        System.Guid targetUserId,
        System.Collections.Generic.IReadOnlyCollection<System.Guid> rolesAdded,
        System.Collections.Generic.IReadOnlyCollection<System.Guid> rolesRemoved,
        System.Threading.CancellationToken cancellationToken = default);
}
