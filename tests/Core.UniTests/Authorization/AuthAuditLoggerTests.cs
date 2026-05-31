using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// AuthAuditLogger contract: every event must flow through IAppLogger with the
/// canonical EventType + the documented metadata keys. Tests assert on the
/// exact captured metadata so any mutation to the keys, message strings, or
/// metadata composition gets killed. Identifier values must be SHA-256 hashed
/// before logging — the raw value must never appear in metadata. The metadata
/// key literals are duplicated here intentionally: log consumers (queries,
/// alerts, dashboards) bind to these strings, so a rename in source must
/// break the suite.
/// </summary>
public class AuthAuditLoggerTests
{
    private const string KeyEventType = "EventType";
    private const string KeyUserId = "UserId";
    private const string KeyReason = "Reason";
    private const string KeyPath = "Path";
    private const string KeyRequiredPermission = "RequiredPermission";
    private const string KeyIdentifierHash = "IdentifierHash";
    private const string KeyTokenCount = "TokenCount";
    private const string KeyTargetUserId = "TargetUserId";
    private const string KeyRolesAdded = "RolesAdded";
    private const string KeyRolesRemoved = "RolesRemoved";
    private const string Source = "AuthAudit";

    private static (AuthAuditLogger Sut, Mock<IAppLogger> Logger, Mock<IHttpContextAccessor> HttpAccessor) Build(HttpContext? context = null)
    {
        var logger = new Mock<IAppLogger>();
        var http = new Mock<IHttpContextAccessor>();
        http.SetupGet(h => h.HttpContext).Returns(context);
        return (new AuthAuditLogger(logger.Object, http.Object), logger, http);
    }

    [Fact]
    public async Task LogPermissionDenied_WritesWarningWithRequiredMetadata()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        string? capturedMessage = null, capturedSource = null;
        logger.Setup(l => l.LogWarningAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<HttpContext?>(),
                It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((m, s, _, md) =>
            {
                capturedMessage = m; capturedSource = s; captured = md;
            })
            .Returns(Task.CompletedTask);

        var userId = Guid.NewGuid();
        await sut.LogPermissionDeniedAsync(userId, "courses.read", "/api/courses");

        capturedMessage.Should().Be("Permission denied.");
        capturedSource.Should().Be(Source);
        captured.Should().NotBeNull();
        captured![KeyEventType].Should().Be(AuthAuditEventTypes.PermissionDenied);
        captured[KeyUserId].Should().Be(userId);
        captured[KeyRequiredPermission].Should().Be("courses.read");
        captured[KeyPath].Should().Be("/api/courses");
    }

    [Fact]
    public async Task LogPermissionDenied_PathFromHttpContextFallback_WhenArgumentMissing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/auto";
        var (sut, logger, _) = Build(ctx);
        Dictionary<string, object>? captured = null;
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), ctx, It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((_, _, _, md) => captured = md)
            .Returns(Task.CompletedTask);

        await sut.LogPermissionDeniedAsync(Guid.NewGuid(), "perm");

        captured.Should().NotBeNull();
        captured![KeyPath].Should().Be("/api/auto");
    }

    [Fact]
    public async Task LogPermissionDenied_PathEmpty_WhenNoArgumentAndNoHttpContext()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((_, _, _, md) => captured = md)
            .Returns(Task.CompletedTask);

        await sut.LogPermissionDeniedAsync(Guid.NewGuid(), "perm");

        captured.Should().NotBeNull();
        captured![KeyPath].Should().Be(string.Empty);
    }

    [Fact]
    public async Task LogAuthenticationFailed_HashesIdentifier_NotLeakingRawValue()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        string? capturedMessage = null;
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((m, _, _, md) => { capturedMessage = m; captured = md; })
            .Returns(Task.CompletedTask);

        const string rawIdentifier = "victim@example.com";
        await sut.LogAuthenticationFailedAsync(rawIdentifier, "bad-credentials");

        capturedMessage.Should().Be("Authentication failed.");
        captured.Should().NotBeNull();
        captured![KeyEventType].Should().Be(AuthAuditEventTypes.AuthFailed);
        captured[KeyReason].Should().Be("bad-credentials");
        var hash = (string)captured[KeyIdentifierHash];
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
        hash.Should().NotContain(rawIdentifier);
        captured.Values.Should().NotContain(rawIdentifier);
    }

    [Fact]
    public async Task LogAuthenticationFailed_SameIdentifier_ProducesSameHash()
    {
        var (sut, logger, _) = Build();
        var hashes = new List<string>();
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((_, _, _, md) =>
                hashes.Add((string)md![KeyIdentifierHash]))
            .Returns(Task.CompletedTask);

        await sut.LogAuthenticationFailedAsync("same", "r1");
        await sut.LogAuthenticationFailedAsync("same", "r2");
        await sut.LogAuthenticationFailedAsync("different", "r3");

        hashes.Should().HaveCount(3);
        hashes[0].Should().Be(hashes[1], "identical identifiers must hash to the same value for correlation");
        hashes[2].Should().NotBe(hashes[0], "different identifiers must hash to different values");
    }

    [Fact]
    public async Task LogAuthenticationFailed_NullIdentifier_HashIsEmptyString()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((_, _, _, md) => captured = md)
            .Returns(Task.CompletedTask);

        await sut.LogAuthenticationFailedAsync(null!, "no-identifier");

        captured![KeyIdentifierHash].Should().Be(string.Empty);
    }

    [Fact]
    public async Task LogLogout_WritesInfoWithCanonicalEventType()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        string? capturedMessage = null, capturedSource = null;
        logger.Setup(l => l.LogInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((m, s, _, md) =>
            {
                capturedMessage = m; capturedSource = s; captured = md;
            })
            .Returns(Task.CompletedTask);

        var userId = Guid.NewGuid();
        await sut.LogLogoutAsync(userId);

        capturedMessage.Should().Be("User logged out.");
        capturedSource.Should().Be(Source);
        captured![KeyEventType].Should().Be(AuthAuditEventTypes.Logout);
        captured[KeyUserId].Should().Be(userId);
    }

    [Fact]
    public async Task LogTokenRevoked_CapturesReasonAndCount()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        logger.Setup(l => l.LogInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((_, _, _, md) => captured = md)
            .Returns(Task.CompletedTask);

        var userId = Guid.NewGuid();
        await sut.LogTokenRevokedAsync(userId, "password-rotated", 5);

        captured![KeyEventType].Should().Be(AuthAuditEventTypes.TokenRevoked);
        captured[KeyUserId].Should().Be(userId);
        captured[KeyReason].Should().Be("password-rotated");
        captured[KeyTokenCount].Should().Be(5);
    }

    [Fact]
    public async Task LogRefreshReplay_WritesWarning()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        string? capturedMessage = null;
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((m, _, _, md) => { capturedMessage = m; captured = md; })
            .Returns(Task.CompletedTask);

        var userId = Guid.NewGuid();
        await sut.LogRefreshReplayAsync(userId);

        capturedMessage.Should().Contain("replay");
        captured![KeyEventType].Should().Be(AuthAuditEventTypes.RefreshReplay);
        captured[KeyUserId].Should().Be(userId);
    }

    [Fact]
    public async Task LogRoleAssignmentChanged_PreservesAddedAndRemovedSets()
    {
        var (sut, logger, _) = Build();
        Dictionary<string, object>? captured = null;
        logger.Setup(l => l.LogInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string, HttpContext?, Dictionary<string, object>?>((_, _, _, md) => captured = md)
            .Returns(Task.CompletedTask);

        var targetId = Guid.NewGuid();
        var added = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var removed = new[] { Guid.NewGuid() };
        await sut.LogRoleAssignmentChangedAsync(targetId, added, removed);

        captured![KeyEventType].Should().Be(AuthAuditEventTypes.RoleAssignmentChanged);
        captured[KeyTargetUserId].Should().Be(targetId);
        captured[KeyRolesAdded].Should().BeEquivalentTo(added);
        captured[KeyRolesRemoved].Should().BeEquivalentTo(removed);
    }

    [Fact]
    public async Task SwallowsDownstreamLoggerExceptions_DoesNotBubbleToAuthHotPath()
    {
        var (sut, logger, _) = Build();
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext?>(), It.IsAny<Dictionary<string, object>?>()))
            .ThrowsAsync(new InvalidOperationException("log sink down"));

        var act = async () => await sut.LogPermissionDeniedAsync(Guid.NewGuid(), "p");
        await act.Should().NotThrowAsync("audit failures must never abort the calling auth flow");
    }

    [Theory]
    [InlineData("permission_denied")]
    [InlineData("auth_failed")]
    [InlineData("logout")]
    [InlineData("token_revoked")]
    [InlineData("refresh_replay")]
    [InlineData("role_assignment_changed")]
    public void EventTypeConstants_AreStableAcrossReleases(string expected)
    {
        var constants = new[]
        {
            AuthAuditEventTypes.PermissionDenied,
            AuthAuditEventTypes.AuthFailed,
            AuthAuditEventTypes.Logout,
            AuthAuditEventTypes.TokenRevoked,
            AuthAuditEventTypes.RefreshReplay,
            AuthAuditEventTypes.RoleAssignmentChanged,
        };
        constants.Should().Contain(expected,
            "log consumers (queries, alerts, dashboards) join on these literal strings — renaming them silently breaks observability");
    }
}