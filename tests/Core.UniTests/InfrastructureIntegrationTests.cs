using System.Globalization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Outbox;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using CapitalUniversity.Core.UniTests._TestInfra;

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
        var sut = new EffectiveScope(userScope.Object, db, execContext.Object, new Mock<IRequestContext>().Object);

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

    [Fact]
    public async Task EffectiveScope_Staff_PathPrefixMatch_EnforcesDataIsolation()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        
        // Ensure all module assemblies are registered so EF pick up all configurations.
        ModuleAssemblyRegistration.Ensure(typeof(CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering).Assembly);
        ModuleAssemblyRegistration.Ensure(typeof(CapitalUniversity.Modules.Schedule.Domain.ScheduleSlot).Assembly);
        ModuleAssemblyRegistration.Ensure(typeof(CapitalUniversity.Modules.Payments.Domain.Invoice).Assembly);

        using var db = new CoreDbContext(options);

        // Build a tree: /Univ -> /Univ/Fac1 -> /Univ/Fac1/Dept1
        //                     -> /Univ/Fac2
        var univ = new StructureNode { Id = Guid.NewGuid(), Name = "Univ", Path = "/Univ" };
        var fac1 = new StructureNode { Id = Guid.NewGuid(), ParentId = univ.Id, Name = "Fac1", Path = "/Univ/Fac1" };
        var fac2 = new StructureNode { Id = Guid.NewGuid(), ParentId = univ.Id, Name = "Fac2", Path = "/Univ/Fac2" };
        var dept1 = new StructureNode { Id = Guid.NewGuid(), ParentId = fac1.Id, Name = "Dept1", Path = "/Univ/Fac1/Dept1" };
        db.StructureNodes.AddRange(univ, fac1, fac2, dept1);

        var staffId = Guid.NewGuid();
        var studentInDept1 = new Student { Id = Guid.NewGuid(), StructureNodeId = dept1.Id, StudentCode = "S1", Name = "S1", NationalId = "1", Email = "s1@u.edu" };
        var studentInFac2 = new Student { Id = Guid.NewGuid(), StructureNodeId = fac2.Id, StudentCode = "S2", Name = "S2", NationalId = "2", Email = "s2@u.edu" };
        db.Students.AddRange(studentInDept1, studentInFac2);
        await db.SaveChangesAsync();

        var userScope = new Mock<IUserScope>();
        userScope.Setup(u => u.UserId).Returns(staffId);
        userScope.Setup(u => u.IsStudent).Returns(false);
        userScope.Setup(u => u.HasGlobalScope).Returns(false);
        // Grant access ONLY to Fac1 and its children
        userScope.Setup(u => u.AuthorizedNodePaths).Returns(new HashSet<string> { "/Univ/Fac1" });

        var sut = new EffectiveScope(userScope.Object, db, new Mock<IExecutionContext>().Object, new Mock<IRequestContext>().Object);

        // Act & Assert
        (await sut.CanAccessStructureNodeAsync(dept1.Id)).Should().BeTrue("Staff has grant on parent Fac1.");
        (await sut.CanAccessStructureNodeAsync(fac2.Id)).Should().BeFalse("Staff has no grant covering Fac2.");
        
        (await sut.CanAccessStudentAsync(studentInDept1.Id)).Should().BeTrue("Student is in a node covered by Fac1 grant.");
        (await sut.CanAccessStudentAsync(studentInFac2.Id)).Should().BeFalse("Student is in Fac2 which is outside the Fac1 grant.");
    }

    [Fact]
    public async Task EffectiveScope_Student_AncestorPathMatch_AllowsViewingProgramDetails()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new CoreDbContext(options);

        var univ = new StructureNode { Id = Guid.NewGuid(), Name = "Univ", Path = "/Univ" };
        var fac1 = new StructureNode { Id = Guid.NewGuid(), ParentId = univ.Id, Name = "Fac1", Path = "/Univ/Fac1" };
        var dept1 = new StructureNode { Id = Guid.NewGuid(), ParentId = fac1.Id, Name = "Dept1", Path = "/Univ/Fac1/Dept1" };
        var fac2 = new StructureNode { Id = Guid.NewGuid(), ParentId = univ.Id, Name = "Fac2", Path = "/Univ/Fac2" };
        db.StructureNodes.AddRange(univ, fac1, dept1, fac2);
        await db.SaveChangesAsync();

        var studentId = Guid.NewGuid();
        var userScope = new Mock<IUserScope>();
        userScope.Setup(u => u.UserId).Returns(studentId);
        userScope.Setup(u => u.IsStudent).Returns(true);
        userScope.Setup(u => u.OwnStructureNodePath).Returns("/Univ/Fac1/Dept1");

        var sut = new EffectiveScope(userScope.Object, db, new Mock<IExecutionContext>().Object, new Mock<IRequestContext>().Object);

        // Act & Assert
        (await sut.CanAccessStructureNodeAsync(dept1.Id)).Should().BeTrue("Student is in Dept1.");
        (await sut.CanAccessStructureNodeAsync(fac1.Id)).Should().BeTrue("Student can see ancestor Faculty.");
        (await sut.CanAccessStructureNodeAsync(univ.Id)).Should().BeTrue("Student can see ancestor University.");
        (await sut.CanAccessStructureNodeAsync(fac2.Id)).Should().BeFalse("Student cannot see sibling/unrelated Faculty tree.");
    }
}
