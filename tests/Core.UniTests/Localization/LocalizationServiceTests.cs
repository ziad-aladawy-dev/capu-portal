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
    public void GetFromJson_WithInvalidJson_LogsWarning_AndReturnsDefault()
    {
        // Arrange
        var json = "invalid-json";

        // Act
        var result = _sut.Get<string>(json);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void GetFromJson_WithNullOrEmpty_ReturnsDefault()
    {
        // Act & Assert
        _sut.Get<string>(null!).Should().BeNull();
        _sut.Get<string>("").Should().BeNull();
        _sut.Get<string>("   ").Should().BeNull();
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
