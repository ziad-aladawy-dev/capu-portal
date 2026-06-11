using System;
using System.Net;
using System.Threading.Tasks;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// H10 — proves the Student Academic Hub endpoints are actually MAPPED (the bug
/// was that the SPA's calls 404'd because no backend route existed). A missing
/// route returns 404 even unauthenticated; a mapped route behind the fallback
/// auth policy returns 401. So asserting 401-without-token confirms the route
/// exists and is wired, distinct from the old route-missing 404.
/// </summary>
public class StudentAcademicsApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public StudentAcademicsApiTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_StudentAcademics_" + Guid.NewGuid()));
                services.AddScoped(_ => new Mock<IAppLogger>().Object);
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });
    }

    [Theory]
    [InlineData("transcript")]
    [InlineData("grades/summary")]
    [InlineData("grades/history")]
    [InlineData("registered")]
    public async Task AcademicHubRoutes_AreMapped_AndRequireAuth(string suffix)
    {
        var client = _factory.CreateClient();
        var studentId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/students/{studentId}/{suffix}");

        // Mapped-but-protected → 401 (NOT 404, which would mean the route is missing).
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
