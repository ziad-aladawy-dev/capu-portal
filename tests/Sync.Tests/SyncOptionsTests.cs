using CapitalUniversity.Sync.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

public class SyncOptionsTests
{
    [Fact]
    public void DefaultCronExpression_HasExpectedDefaultValue()
    {
        // Arrange & Act
        var options = new SyncOptions();

        // Assert
        // "0 2 * * *" is Daily at 2 AM
        options.DefaultCronExpression.Should().Be("0 2 * * *");
    }

    [Fact]
    public void DefaultCronExpression_CanBeOverridden()
    {
        // Arrange
        var options = new SyncOptions();
        var customCron = "*/5 * * * *"; // Every 5 minutes

        // Act
        options.DefaultCronExpression = customCron;

        // Assert
        options.DefaultCronExpression.Should().Be(customCron);
    }
}
