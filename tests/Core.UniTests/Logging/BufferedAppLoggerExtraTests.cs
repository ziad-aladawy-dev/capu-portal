using System.Net;
using System.Security.Claims;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Logging;

/// <summary>
/// Additional coverage for the warning/critical levels and the HttpContext
/// snapshot path (user id, method/path, IP-resolution precedence).
/// </summary>
public class BufferedAppLoggerExtraTests
{
    private static async Task<LogEntry> DrainOneAsync(ChannelAuditLogQueue queue)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var entry in queue.ReadAllAsync(cts.Token))
            return entry;
        throw new InvalidOperationException("queue was empty within the timeout");
    }

    [Fact]
    public async Task LogWarningAsync_EnqueuesWarningEntry()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        await sut.LogWarningAsync("warn me", "TestSrc");

        var entry = await DrainOneAsync(queue);
        Assert.Equal(LogLevelType.Warning, entry.Level);
        Assert.Equal("warn me", entry.Message);
    }

    [Fact]
    public async Task LogCriticalAsync_CapturesExceptionMessage()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        await sut.LogCriticalAsync("kaboom", new Exception("inner"), "TestSrc");

        var entry = await DrainOneAsync(queue);
        Assert.Equal(LogLevelType.Critical, entry.Level);
        Assert.Equal("inner", entry.ExceptionMessage);
    }

    [Fact]
    public async Task LogInfoAsync_WithHttpContext_CapturesIpMethodPathUser()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var userId = Guid.NewGuid().ToString();
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/things";
        http.Request.Method = "POST";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }));
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        await sut.LogInfoAsync("with-context", "TestSrc", context: http);

        var entry = await DrainOneAsync(queue);
        Assert.Equal("/api/things", entry.RequestPath);
        Assert.Equal("POST", entry.HttpMethod);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal("10.0.0.5", entry.IpAddress);
    }

    [Fact]
    public async Task LogInfoAsync_XForwardedFor_PrefersFirstHop()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var http = new DefaultHttpContext();
        http.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 10.0.0.1";
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        await sut.LogInfoAsync("fwd", "TestSrc", context: http);

        var entry = await DrainOneAsync(queue);
        Assert.Equal("203.0.113.7", entry.IpAddress);
    }

    [Fact]
    public async Task LogInfoAsync_XRealIp_UsedWhenForwardedForAbsent()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var http = new DefaultHttpContext();
        http.Request.Headers["X-Real-IP"] = "198.51.100.9";

        await sut.LogInfoAsync("real", "TestSrc", context: http);

        var entry = await DrainOneAsync(queue);
        Assert.Equal("198.51.100.9", entry.IpAddress);
    }

    [Fact]
    public async Task LogInfoAsync_IPv6Loopback_NormalisedToIPv4()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;

        await sut.LogInfoAsync("loopback", "TestSrc", context: http);

        var entry = await DrainOneAsync(queue);
        Assert.Equal("127.0.0.1", entry.IpAddress);
    }
}