using System.Globalization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AppExecutionContext = CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication.ExecutionContext;

namespace CapitalUniversity.Core.UniTests;

public class InfrastructureIntegrationTests
{
    [Fact]
    public void CurrentCultureService_Language_FallsBackToAmbientCulture_WhenHttpContextIsNull()
    {
        // Arrange
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns((HttpContext)null!);
        var sut = new CurrentCultureService(http.Object);

        // Act & Assert
        using (new SystemCultureScope("en"))
        {
            sut.Language.Should().Be("en");
        }

        using (new SystemCultureScope("ar"))
        {
            sut.Language.Should().Be("ar");
        }
    }

    [Fact]
    public void CurrentCultureService_Language_PrefersHttpContext_WhenAvailable()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Language"] = "en-US,en;q=0.9";
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(context);
        var sut = new CurrentCultureService(http.Object);

        // Act & Assert
        using (new SystemCultureScope("ar"))
        {
            // HttpContext "en" should win over ambient "ar"
            sut.Language.Should().Be("en");
        }
    }

    [Fact]
    public async Task OutboxService_Capture_CorrelationAndCulture()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new CoreDbContext(options);
        
        var execContext = new Mock<IExecutionContext>();
        execContext.Setup(e => e.RequestId).Returns("test-correlation-id");
        
        var cultureService = new Mock<ICurrentCultureService>();
        cultureService.Setup(c => c.Language).Returns("en");

        var sut = new OutboxService(db, execContext.Object, cultureService.Object);

        // Act
        await sut.EnqueueAsync("test.type", new { Data = "test" });
        await db.SaveChangesAsync();

        // Assert
        db.OutboxMessages.Count().Should().Be(1);
        var msg = db.OutboxMessages.First();
        msg.CorrelationId.Should().Be("test-correlation-id");
        msg.Culture.Should().Be("en");
    }

    [Fact]
    public async Task EffectiveScope_AllowsAccess_InSystemMode()
    {
        // Arrange
        var userScope = new Mock<IUserScope>();
        var options = new DbContextOptionsBuilder<CoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new CoreDbContext(options);
        var execContext = new Mock<IExecutionContext>();
        
        execContext.Setup(e => e.IsSystem).Returns(true);
        var sut = new EffectiveScope(userScope.Object, db, execContext.Object);

        // Act
        var canAccessNode = await sut.CanAccessStructureNodeAsync(Guid.NewGuid());
        var canAccessStudent = await sut.CanAccessStudentAsync(Guid.NewGuid());

        // Assert
        canAccessNode.Should().BeTrue("System mode must bypass normal node scope checks.");
        canAccessStudent.Should().BeTrue("System mode must bypass normal student scope checks.");
        userScope.Verify(u => u.EnsureLoadedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SystemExecutionScope_TogglesFlag_AndRestoresOnDispose()
    {
        // Arrange
        var http = new Mock<IHttpContextAccessor>();
        var execContext = new AppExecutionContext(http.Object);

        // Act & Assert
        execContext.IsSystem.Should().BeFalse();

        using (new SystemExecutionScope(execContext))
        {
            execContext.IsSystem.Should().BeTrue();
            
            using (new SystemExecutionScope(execContext))
            {
                execContext.IsSystem.Should().BeTrue();
            }
            
            execContext.IsSystem.Should().BeTrue();
        }

        execContext.IsSystem.Should().BeFalse();
    }
}
