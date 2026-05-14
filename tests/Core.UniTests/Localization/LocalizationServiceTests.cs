using System;
using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Localization;

public class LocalizationServiceTests
{
    private readonly Mock<ICurrentCultureService> _mockCultureService;
    private readonly LocalizationService _sut;

    public LocalizationServiceTests()
    {
        _mockCultureService = new Mock<ICurrentCultureService>();
        _sut = new LocalizationService(_mockCultureService.Object);
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

    private enum TestEnum
    {
        [Localized("قيمة بالعربية", "Value in English")]
        ValueWithAttribute,

        ValueWithoutAttribute
    }
}
