using CapitalUniversity.Core.Infrastructure.Logging;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Logging;

/// <summary>
/// D3 contract: every plausible secret shape is redacted before audit logs are
/// persisted. Conservative > clever — false positives are fine, leaks are not.
/// </summary>
public class LogScrubberTests
{
    [Fact]
    public void ScrubMessage_JwtInsideMessage_IsRedacted()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        var input = $"login failed for user X, token={jwt}";

        var output = LogScrubber.ScrubMessage(input);

        output.Should().NotContain(jwt);
        output.Should().Contain(LogScrubber.RedactedPlaceholder);
    }

    [Fact]
    public void ScrubMessage_BearerHeader_IsRedacted()
    {
        var input = "Authorization: Bearer abcdef.ghijkl.mnopqr received";

        var output = LogScrubber.ScrubMessage(input);

        output.Should().NotContain("abcdef.ghijkl.mnopqr");
        output.Should().Contain($"Bearer {LogScrubber.RedactedPlaceholder}");
    }

    [Fact]
    public void ScrubMessage_RefreshTokenShape_IsRedacted()
    {
        // 43 base64url chars — matches RefreshTokenService.GenerateRawToken output.
        var refreshToken = "uH9k_Dz1aHJqfNbY1xVw2nT5p0eXqMrYL_pa3zKfg8M";
        var input = $"refresh requested with token {refreshToken}";

        var output = LogScrubber.ScrubMessage(input);

        output.Should().NotContain(refreshToken);
        output.Should().Contain(LogScrubber.RedactedPlaceholder);
    }

    [Fact]
    public void ScrubMessage_BenignText_IsUnchanged()
    {
        const string input = "User 123e4567-e89b-12d3-a456-426614174000 logged in successfully.";
        var output = LogScrubber.ScrubMessage(input);
        output.Should().Be(input, "GUIDs contain dashes but no dot-separated triple, so they should survive");
    }

    [Fact]
    public void ScrubMessage_Null_ReturnsNull()
    {
        LogScrubber.ScrubMessage(null).Should().BeNull();
    }

    [Fact]
    public void ScrubMetadata_SensitiveKeys_AreFullyRedacted()
    {
        var metadata = new Dictionary<string, object>
        {
            { "userId", Guid.NewGuid() },
            { "Password", "hunter2" },
            { "CurrentPassword", "hunter2" },
            { "newPassword", "betterPass!" },
            { "refreshToken", "doesnt-matter" },
            { "Authorization", "Bearer foo" },
            { "Cookie", "session=abc" },
            { "Set-Cookie", "session=abc" },
            { "api_key", "k1" },
            { "X-API-KEY", "k1" },
            { "apiKey", "k1" },
            { "client_secret", "shhh" },
        };

        var scrubbed = LogScrubber.ScrubMetadata(metadata)!;

        scrubbed["userId"].Should().NotBe(LogScrubber.RedactedPlaceholder, "userId is not sensitive");
        scrubbed["Password"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["CurrentPassword"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["newPassword"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["refreshToken"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["Authorization"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["Cookie"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["Set-Cookie"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["api_key"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["X-API-KEY"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["apiKey"].Should().Be(LogScrubber.RedactedPlaceholder);
        scrubbed["client_secret"].Should().Be(LogScrubber.RedactedPlaceholder);
    }

    [Fact]
    public void ScrubMetadata_NonSensitiveStringValue_StillScrubbedForSecretShapes()
    {
        var metadata = new Dictionary<string, object>
        {
            { "errorDetail", "Failed: Bearer xyz123.abc456.qwerty789 rejected" },
        };

        var scrubbed = LogScrubber.ScrubMetadata(metadata)!;

        scrubbed["errorDetail"].ToString().Should().NotContain("xyz123.abc456.qwerty789");
        scrubbed["errorDetail"].ToString().Should().Contain(LogScrubber.RedactedPlaceholder);
    }

    [Fact]
    public void ScrubMetadata_Null_ReturnsNull()
    {
        LogScrubber.ScrubMetadata(null).Should().BeNull();
    }
}
