using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
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
        var dbName = "SemesterTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<CoreDbContext>));
                services.RemoveAll(typeof(CoreDbContext));

                services.AddDbContext<CoreDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });

                services.RemoveAll(typeof(CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger));
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

    [Fact]
    public async Task Create_NonExistentAcademicYear_ReturnsBadRequest()
    {
        // Arrange
        var semesterRequest = new CreateSemesterRequest
        {
            AcademicYearId = Guid.NewGuid(),
            Name = "Ghost Semester",
            Order = 1,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(4)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/semesters", semesterRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_OverlappingSemesters_ReturnsBadRequest()
    {
        // Arrange
        var yearRequest = new CreateAcademicYearRequest
        {
            Name = "Year 2025",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31)
        };
        var yearResp = await _client.PostAsJsonAsync("/api/academic-years", yearRequest);
        Guid yearId = await GetIdFromResponse(yearResp);

        var sem1 = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Sem 1",
            Order = 1,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 6, 30)
        };
        await _client.PostAsJsonAsync("/api/semesters", sem1);

        var sem2 = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Sem 2",
            Order = 2,
            StartDate = new DateTime(2025, 6, 1), // Overlap
            EndDate = new DateTime(2025, 12, 31)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/semesters", sem2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_SemesterDate_OutsideYearRange_ReturnsBadRequest()
    {
        // Arrange
        var yearRequest = new CreateAcademicYearRequest
        {
            Name = "Year 2026",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };
        var yearResp = await _client.PostAsJsonAsync("/api/academic-years", yearRequest);
        Guid yearId = await GetIdFromResponse(yearResp);

        var semRequest = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Sem 1",
            Order = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 6, 30)
        };
        var semResp = await _client.PostAsJsonAsync("/api/semesters", semRequest);
        Guid semId = await GetIdFromResponse(semResp);

        var updateRequest = new UpdateSemesterRequest
        {
            EndDate = new DateTime(2027, 1, 1) // Outside year
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/semesters/{semId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
