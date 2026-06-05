using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using CapitalUniversity.API;
using Moq;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Contract.Tests;

public class AuthenticationApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public AuthenticationApiTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Replace SQL Server with In-Memory
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();

                services.AddDbContext<CoreDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_Auth_" + Guid.NewGuid().ToString());
                });

                services.AddScoped(_ => new Mock<IAppLogger>().Object);
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });
    }

    [Fact]
    public async Task GetPermissions_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/permissions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}