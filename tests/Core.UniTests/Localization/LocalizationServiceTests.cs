using System;
using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Localization;

public class LocalizationServiceTests
{
    private readonly Mock<ICurrentCultureService> _mockCultureService;
    private readonly Mock<ILogger<LocalizationService>> _mockLogger;
    private readonly LocalizationService _sut;

    public LocalizationServiceTests()
    {
        _mockCultureService = new Mock<ICurrentCultureService>();
        _mockLogger = new Mock<ILogger<LocalizationService>>();
        _sut = new LocalizationService(_mockCultureService.Object, _mockLogger.Object);
    }

    [Theory]
    [InlineData("ar", "مرحبا")]
    [InlineData("en", "Hello")]
    public void GetFromJson_ReturnsCorrectValue_BasedOnCulture(string language, string expectedValue)
    {
        // Arrange
        _mockCultureService.Setup(c => c.Language).Returns(language);
        var json = @"{ ""ar"": ""مرحبا"", ""en"": ""Hello"" }";

        // Act
        var result = _sut.Get<string>(json);

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public void GetFromJson_WhenLanguageMissing_FallsBackToArabic()
    {
        // Arrange
        _mockCultureService.Setup(c => c.Language).Returns("fr");
        var json = @"{ ""ar"": ""مرحبا"", ""en"": ""Hello"" }";

        // Act
        var result = _sut.Get<string>(json);

        // Assert
        result.Should().Be("مرحبا"); // Default language is "ar"
    }

    [Fact]
    public void GetFromEnum_WithLocalizedAttribute_ReturnsArabic_WhenCultureIsAr()
    {
        // Arrange
        _mockCultureService.Setup(c => c.Language).Returns("ar");

        // Act
        var result = _sut.Get(TestEnum.ValueWithAttribute);

        // Assert
        result.Should().Be("قيمة بالعربية");
    }

    [Fact]
    public void GetFromEnum_WithLocalizedAttribute_ReturnsEnglish_WhenCultureIsEn()
    {
        // Arrange
        _mockCultureService.Setup(c => c.Language).Returns("en");

        // Act
        var result = _sut.Get(TestEnum.ValueWithAttribute);

        // Assert
        result.Should().Be("Value in English");
    }

    [Fact]
    public void GetFromEnum_WithoutLocalizedAttribute_ReturnsEnumName()
    {
        // Arrange
        _mockCultureService.Setup(c => c.Language).Returns("ar");

        // Act
        var result = _sut.Get(TestEnum.ValueWithoutAttribute);

        // Assert
        result.Should().Be(nameof(TestEnum.ValueWithoutAttribute));
    }

    [Fact]
    public void GetString_ReturnsKeyAsPassThrough()
    {
        // Arrange
        var key = "SomeKey";

        // Act
        var result = _sut.GetString(key);

        // Assert
        result.Should().Be(key);
    }

    [Fact]
    public void GetFromJson_WithPlainText_ReturnsLiteral_WhenTIsString()
    {
        // The Get<T>(json) resolver is the single read path for fields that
        // store {"ar":"…","en":"…"} JSON dicts AND for legacy rows that still
        // carry a plain-text literal seeded before the JSON shape landed.
        // Returning the literal (rather than null with a warning) is the
        // backwards-compatibility contract — UIs see "the value", not blanks.
        var json = "invalid-json";

        var result = _sut.Get<string>(json);

        result.Should().Be("invalid-json");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never);
    }

    [Fact]
    public void GetFromJson_WithNullOrEmpty_StringT_PreservesEmpty()
    {
        // For string T the resolver preserves the input shape — an empty
        // entity column stays empty rather than collapsing to null. Non-string
        // generic parameters still get default(T). See LocalizationService.Get<T>
        // for the rationale (DTO consumers don't expect a null Name when the
        // underlying value is just empty).
        _sut.Get<string>(null!).Should().BeEmpty();
        _sut.Get<string>("").Should().BeEmpty();
        _sut.Get<string>("   ").Should().Be("   ");
        _sut.Get<Dictionary<string, string>>(null!).Should().BeNull();
    }

    [Fact]
    public void GetFromEnum_WithNullValue_ReturnsEmptyString()
    {
        // Act
        var result = _sut.Get(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetFromJson_WhenOnlyEnglishAvailable_AndCultureIsAr_FallsBackToEn()
    {
        // Arrange
        _mockCultureService.Setup(c => c.Language).Returns("ar");
        var json = @"{ ""en"": ""Hello"" }";

        // Act
        var result = _sut.Get<string>(json);

        // Assert
        result.Should().Be("Hello");
    }

    private enum TestEnum
    {
        [Localized("قيمة بالعربية", "Value in English")]
        ValueWithAttribute,

        ValueWithoutAttribute
    }
}