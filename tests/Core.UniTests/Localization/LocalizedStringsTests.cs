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
    public static IEnumerable<object[]> AllShippedKeys() => new[]
    {
        new object[] { LocalizedKeys.Auth.Unauthorized },
        new object[] { LocalizedKeys.Auth.InvalidCredentials },
        new object[] { LocalizedKeys.Auth.SessionExpired },
        new object[] { LocalizedKeys.Auth.TokenInvalid },
        new object[] { LocalizedKeys.Auth.PasswordChangeFailed },
        new object[] { LocalizedKeys.Permissions.Forbidden },
        new object[] { LocalizedKeys.Infrastructure.ValidationError },
        new object[] { LocalizedKeys.Infrastructure.NotFound },
        new object[] { LocalizedKeys.Infrastructure.Conflict },
        new object[] { LocalizedKeys.Infrastructure.ServerError },
        new object[] { LocalizedKeys.Infrastructure.DuplicateKey },
        new object[] { LocalizedKeys.Infrastructure.ForeignKeyViolation },
        new object[] { LocalizedKeys.Courses.NotFound },
        new object[] { LocalizedKeys.Courses.CodeInUse },
        new object[] { LocalizedKeys.Courses.CreditHoursOutOfRange },
        new object[] { LocalizedKeys.Courses.PlanNotFound },
        new object[] { LocalizedKeys.Courses.PlanCourseEntryNotFound },
        new object[] { LocalizedKeys.Courses.PlanCourseAlreadyPresent },
        new object[] { LocalizedKeys.Courses.StructureNodeNotFound },
        new object[] { LocalizedKeys.Courses.EffectiveToAfterFrom },
        new object[] { LocalizedKeys.Semesters.NotFound },
        new object[] { LocalizedKeys.Semesters.AcademicYearNotFound },
        new object[] { LocalizedKeys.Semesters.AcademicYearMissing },
        new object[] { LocalizedKeys.Semesters.DatesOverlap },
        new object[] { LocalizedKeys.Semesters.DatesOutsideAcademicYear },
        new object[] { LocalizedKeys.Semesters.EndAfterStart },
        new object[] { LocalizedKeys.Semesters.YearDatesOverlap },
        new object[] { LocalizedKeys.Payments.InvoiceNotFound },
        new object[] { LocalizedKeys.Payments.StudentNotFound },
        new object[] { LocalizedKeys.Payments.AtLeastOneItem },
        new object[] { LocalizedKeys.Payments.PaidCannotCancel },
        new object[] { LocalizedKeys.StudentInformation.ProfileRecordNotFound },
        new object[] { LocalizedKeys.StudentInformation.StudentNotFound },
        new object[] { LocalizedKeys.StudentInformation.InvalidJson },
        new object[] { LocalizedKeys.StudentInformation.CustomCategoryKeyRequired },
    };

    [Theory]
    [MemberData(nameof(AllShippedKeys))]
    public void Resolve_ShippedKey_HasArabicTranslation(string key)
    {
        var resolved = LocalizedStrings.Resolve(key, "ar");
        resolved.Should().NotBe(key, $"key '{key}' must have an Arabic translation registered");
        resolved.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllShippedKeys))]
    public void Resolve_ShippedKey_HasEnglishTranslation(string key)
    {
        var resolved = LocalizedStrings.Resolve(key, "en");
        resolved.Should().NotBe(key, $"key '{key}' must have an English translation registered");
        resolved.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllShippedKeys))]
    public void Resolve_ShippedKey_ArabicAndEnglishDiffer(string key)
    {
        var ar = LocalizedStrings.Resolve(key, "ar");
        var en = LocalizedStrings.Resolve(key, "en");
        ar.Should().NotBe(en, $"key '{key}' must have distinct translations for ar and en");
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
    public void ContainsKey_ReturnsTrue_ForRegisteredKeys()
    {
        LocalizedStrings.ContainsKey(LocalizedKeys.Courses.NotFound).Should().BeTrue();
        LocalizedStrings.ContainsKey(LocalizedKeys.Payments.InvoiceNotFound).Should().BeTrue();
    }

    [Fact]
    public void ContainsKey_ReturnsFalse_ForUnknownOrEmpty()
    {
        LocalizedStrings.ContainsKey("not.a.real.key").Should().BeFalse();
        LocalizedStrings.ContainsKey("").Should().BeFalse();
        LocalizedStrings.ContainsKey(null).Should().BeFalse();
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

    [Fact]
    public void LocalizationService_ContainsKey_DelegatesToCatalogue()
    {
        var culture = new Mock<ICurrentCultureService>();
        culture.Setup(c => c.Language).Returns("en");
        var sut = new LocalizationService(culture.Object, NullLogger<LocalizationService>.Instance);

        sut.ContainsKey(LocalizedKeys.Courses.NotFound).Should().BeTrue();
        sut.ContainsKey("nope").Should().BeFalse();
    }
}