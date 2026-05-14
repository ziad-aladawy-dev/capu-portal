using System;
using System.Diagnostics;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CapitalUniversity.Core.UniTests.Performance;

public class LocalizationPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ICurrentCultureService> _mockCulture;
    private readonly Mock<ILogger<LocalizationService>> _mockLogger;
    private readonly LocalizationService _sut;

    public LocalizationPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockCulture = new Mock<ICurrentCultureService>();
        _mockLogger = new Mock<ILogger<LocalizationService>>();
        _sut = new LocalizationService(_mockCulture.Object, _mockLogger.Object);
        _mockCulture.Setup(c => c.Language).Returns("ar");
    }

    [Fact]
    public void Benchmark_EnumLocalization_WithCaching()
    {
        const int iterations = 10000;
        
        // Warm up / Force first reflection
        _sut.Get(TestPerfEnum.Value1);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _sut.Get(TestPerfEnum.Value1);
            _sut.Get(TestPerfEnum.Value2);
        }
        sw.Stop();

        _output.WriteLine($"Time for {iterations * 2} enum lookups: {sw.Elapsed.TotalMilliseconds}ms");
        _output.WriteLine($"Average time per lookup: {sw.Elapsed.TotalMilliseconds / (iterations * 2)}ms");

        Assert.True(sw.Elapsed.TotalMilliseconds < 100, "Performance should be very high with caching (under 100ms for 20k lookups)");
    }

    private enum TestPerfEnum
    {
        [Localized("عربي 1", "English 1")]
        Value1,
        [Localized("عربي 2", "English 2")]
        Value2
    }
}
