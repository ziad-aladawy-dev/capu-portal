using System.Net;
using System.Net.Http.Json;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using Moq;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace CapitalUniversity.Contract.Tests;

public class AcademicYearApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;
    private readonly HttpClient _client;

    public AcademicYearApiTests(WebApplicationFactory<ModulesRegistry> factory)
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
                    options.UseInMemoryDatabase("AcademicYearTestDb_" + Guid.NewGuid());
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
        mockUser.Setup(u => u.Name).Returns("Admin User");

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
    public async Task Create_ValidYear_ReturnsCreated()
    {
        // Arrange
        var request = new CreateAcademicYearRequest
        {
            Name = "Year 2025-2026",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(300)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/academic-years", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await GetIdFromResponse(response);
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_OverlappingYear_ReturnsBadRequest()
    {
        // Arrange
        var year1 = new CreateAcademicYearRequest
        {
            Name = "Year 1",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(100)
        };
        var resp1 = await _client.PostAsJsonAsync("/api/academic-years", year1);
        resp1.EnsureSuccessStatusCode();

        var year2 = new CreateAcademicYearRequest
        {
            Name = "Year 2",
            StartDate = DateTime.UtcNow.AddDays(50),
            EndDate = DateTime.UtcNow.AddDays(150)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/academic-years", year2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_PartialUpdate_ShouldPreserveOmittedFields()
    {
        // Arrange
        var createRequest = new CreateAcademicYearRequest
        {
            Name = "Original Name",
            StartDate = DateTime.UtcNow.AddDays(500),
            EndDate = DateTime.UtcNow.AddDays(800)
        };
        var createResponse = await _client.PostAsJsonAsync("/api/academic-years", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Guid id = await GetIdFromResponse(createResponse);

        var patchRequest = new UpdateAcademicYearRequest
        {
            Name = "Updated Name"
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/academic-years/{id}", patchRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var getResponse = await _client.GetAsync($"/api/academic-years/{id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<AcademicYearResponse>();
        
        updated!.Name.Should().Be("Updated Name");
        updated.StartDate.Should().BeCloseTo(createRequest.StartDate, TimeSpan.FromSeconds(1));
        updated.EndDate.Should().BeCloseTo(createRequest.EndDate, TimeSpan.FromSeconds(1));
    }
}
