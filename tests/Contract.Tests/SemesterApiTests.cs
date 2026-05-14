using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CapitalUniversity.Contract.Tests;

public class SemesterApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;
    private readonly HttpClient _client;

    public SemesterApiTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<CoreDbContext>(options =>
                {
                    options.UseInMemoryDatabase("SemesterTestDb_" + Guid.NewGuid());
                });

                var loggerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger));
                if (loggerDescriptor != null) services.Remove(loggerDescriptor);
                services.AddScoped(_ => Mock.Of<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>());
            });
        });

        _client = _factory.CreateClient();
        SetupAuth();
    }

    private void SetupAuth()
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var mockUser = new Mock<IUserCredential>();
        mockUser.Setup(u => u.Id).Returns(Guid.NewGuid());
        mockUser.Setup(u => u.Identifier).Returns("admin");
        mockUser.Setup(u => u.Role).Returns("Admin");

        var token = tokenService.GenerateToken(mockUser.Object);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Guid> GetIdFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_SemesterOutsideYearRange_ReturnsBadRequest()
    {
        // Arrange
        var yearRequest = new CreateAcademicYearRequest
        {
            Name = "Year",
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 6, 30)
        };
        var yearResp = await _client.PostAsJsonAsync("/api/academic-years", yearRequest);
        Guid yearId = await GetIdFromResponse(yearResp);

        var semesterRequest = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Invalid Semester",
            Order = 1,
            StartDate = new DateTime(2025, 8, 1), // Before year start
            EndDate = new DateTime(2025, 12, 31)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/semesters", semesterRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
