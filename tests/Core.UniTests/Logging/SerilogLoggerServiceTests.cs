using System;
using CapitalUniversity.Core.Abstractions.Execution;
using CapitalUniversity.Core.Application.Audit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Logging;

public class SerilogLoggerServiceTests
{
    private readonly Mock<ILogger<SerilogLoggerService>> _mockLogger;
    private readonly Mock<IExecutionContext> _mockContext;
    private readonly SerilogLoggerService _sut;

    public SerilogLoggerServiceTests()
    {
        _mockLogger = new Mock<ILogger<SerilogLoggerService>>();
        _mockContext = new Mock<IExecutionContext>();

        // Set up dummy context values
        _mockContext.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _mockContext.Setup(c => c.ActiveRoleId).Returns(Guid.NewGuid());
        _mockContext.Setup(c => c.RequestId).Returns("Req-123");
        _mockContext.Setup(c => c.Operation).Returns("TestOperation");

        _sut = new SerilogLoggerService(_mockLogger.Object, _mockContext.Object);
    }

    [Fact]
    public void LogInformation_LogsToUnderlyingLogger_WithContextData()
    {
        // Arrange
        var message = "Test Information Message";

        // Act
        _sut.LogInformation(message);

        // Assert
        VerifyLogWasCalled(LogLevel.Information, message);
    }

    [Fact]
    public void LogWarning_LogsToUnderlyingLogger_WithContextData()
    {
        // Arrange
        var message = "Test Warning Message";

        // Act
        _sut.LogWarning(message);

        // Assert
        VerifyLogWasCalled(LogLevel.Warning, message);
    }

    [Fact]
    public void LogError_WithoutException_LogsToUnderlyingLogger()
    {
        // Arrange
        var message = "Test Error Message";

        // Act
        _sut.LogError(message);

        // Assert
        VerifyLogWasCalled(LogLevel.Error, message);
    }

    [Fact]
    public void LogError_WithException_LogsToUnderlyingLogger_WithException()
    {
        // Arrange
        var message = "Test Error Message";
        var exception = new InvalidOperationException("Test Exception");

        // Act
        _sut.LogError(message, exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message) && v.ToString()!.Contains("Req-123")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLogWasCalled(LogLevel logLevel, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message) && v.ToString()!.Contains("Req-123")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
