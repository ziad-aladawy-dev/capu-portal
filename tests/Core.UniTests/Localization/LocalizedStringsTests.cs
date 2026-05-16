using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Localization;

/// <summary>
/// E1 contract: every <see cref="LocalizedKeys"/> entry resolves to a non-key string
/// in every shipped culture, and the resolver degrades gracefully when a culture or
/// key is missing.
/// </summary>
public class LocalizedStringsTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void Resolve_ShippedKeysInShippedCultures_ReturnsTranslatedValue(string culture)
    {
        var keys = new[]
        {
            LocalizedKeys.Auth.Unauthorized,
            LocalizedKeys.Auth.InvalidCredentials,
            LocalizedKeys.Auth.SessionExpired,
            LocalizedKeys.Auth.TokenInvalid,
            LocalizedKeys.Auth.PasswordChangeFailed,
            LocalizedKeys.Permissions.Forbidden,
            LocalizedKeys.Infrastructure.ValidationError,
            LocalizedKeys.Infrastructure.NotFound,
            LocalizedKeys.Infrastructure.Conflict,
            LocalizedKeys.Infrastructure.ServerError,
        };

        foreach (var key in keys)
        {
            var resolved = LocalizedStrings.Resolve(key, culture);
            resolved.Should().NotBe(key,
                $"key '{key}' must have a translation registered for culture '{culture}'");
            resolved.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Resolve_UnknownCulture_FallsBackToDefault()
    {
        var resolved = LocalizedStrings.Resolve(LocalizedKeys.Auth.Unauthorized, "fr");
        var arabic = LocalizedStrings.Resolve(LocalizedKeys.Auth.Unauthorized, "ar");
        resolved.Should().Be(arabic, "unknown cultures must fall through to the default ('ar')");
    }

    [Fact]
    public void Resolve_UnknownKey_ReturnsKeyAsFallback()
    {
        var resolved = LocalizedStrings.Resolve("not.a.real.key", "en");
        resolved.Should().Be("not.a.real.key",
            "missing-translation fallback must surface the key rather than throw or return empty");
    }

    [Fact]
    public void Resolve_NullOrEmptyKey_ReturnsEmpty()
    {
        LocalizedStrings.Resolve("", "en").Should().BeEmpty();
        LocalizedStrings.Resolve(null!, "en").Should().BeEmpty();
    }

    [Fact]
    public void LocalizationService_GetString_HonoursCurrentCulture()
    {
        var arabic = new Mock<ICurrentCultureService>();
        arabic.Setup(c => c.Language).Returns("ar");
        var english = new Mock<ICurrentCultureService>();
        english.Setup(c => c.Language).Returns("en");

        var ar = new LocalizationService(arabic.Object, NullLogger<LocalizationService>.Instance);
        var en = new LocalizationService(english.Object, NullLogger<LocalizationService>.Instance);

        ar.GetString(LocalizedKeys.Auth.Unauthorized).Should().NotBe(en.GetString(LocalizedKeys.Auth.Unauthorized),
            "different cultures must produce different translations for the same key");
    }
}
