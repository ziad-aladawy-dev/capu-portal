using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.Http.Json;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
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
        var dbName = "AcademicYearTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();

                services.AddDbContext<CoreDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });

                services.RemoveAll<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>();
                services.AddScoped(_ => Mock.Of<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>());
            });
        });

        _client = _factory.CreateClient();
        SetupAuth();
    }

    private void SetupAuth()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        
        var userId = Guid.NewGuid();
        
        // Seed the consolidated academic-timeline permission — the single Service +
        // RolePermission row produces canonical "academics.academic-years.*", which
        // gates BOTH AcademicYearsController and SemestersController via
        // PermissionNames.AcademicTimeline.*.
        var module = new Module { Id = Guid.NewGuid(), DisplayName = "Academic Timeline", ModuleKey = "academics" };
        var service = new Service { Id = Guid.NewGuid(), Module = module, DisplayName = "Academic Timeline" };
        var role = new Role { Id = Guid.NewGuid(), Name = "AdminRole" };

        db.Modules.Add(module);
        db.Services.Add(service);
        db.Roles.Add(role);
        db.RolePermissions.Add(new RolePermission(role.Id, service.Id, "academic-years", ActionLevel.Delete)); // Level 5 covers all
        db.StaffRoles.Add(new StaffRoleAssignment(userId, role.Id, "Global", "Global"));

        // SessionVersionMiddleware rejects tokens for users that have no row in
        // Staffs/Students. Seed a matching Staff row so the version check passes
        // (both sides default to 0).
        var anyNodeId = db.StructureNodes.AsNoTracking().Select(n => n.Id).First();
        db.Staffs.Add(new Staff
        {
            Id = userId,
            EmployeeCode = string.Concat("TEST-", userId.ToString("N").AsSpan(0, 8)),
            NationalId = string.Concat("TEST", userId.ToString("N").AsSpan(0, 11)),
            PasswordHash = "test-hash",
            Name = "Test Admin",
            Role = "Admin",
            StructureNodeId = anyNodeId,
            IsActive = true,
        });

        db.SaveChanges();

        var mockUser = new Mock<IUserCredential>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Identifier).Returns("admin");
        mockUser.Setup(u => u.Role).Returns("Admin");
        mockUser.Setup(u => u.Name).Returns("Admin User");

        var token = tokenService.GenerateToken(mockUser.Object);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<Guid> GetIdFromResponse(HttpResponseMessage response)
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
            Name = "Year 2030-2031",
            StartDate = new DateTime(2030, 9, 1),
            EndDate = new DateTime(2031, 8, 31)
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
        // Fixed dates well outside DataSeeder's 2023–2027 seeded years so
        // year1 doesn't collide with the seed (a real failure mode this test
        // used to hit whenever DateTime.UtcNow drifted into the seeded window).
        // Arrange
        var year1 = new CreateAcademicYearRequest
        {
            Name = "Year 1",
            StartDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2030, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        var resp1 = await _client.PostAsJsonAsync("/api/academic-years", year1);
        resp1.EnsureSuccessStatusCode();

        var year2 = new CreateAcademicYearRequest
        {
            Name = "Year 2",
            StartDate = new DateTime(2030, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc)
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

    [Fact]
    public async Task Create_EndDateBeforeStartDate_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateAcademicYearRequest
        {
            Name = "Invalid Dates",
            StartDate = DateTime.UtcNow.AddDays(10),
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/academic-years", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/academic-years/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateAcademicYearRequest { Name = "New Name" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/academic-years/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ValidId_ReturnsOk()
    {
        // Arrange
        var createRequest = new CreateAcademicYearRequest
        {
            Name = "To Delete",
            StartDate = DateTime.UtcNow.AddYears(10),
            EndDate = DateTime.UtcNow.AddYears(11)
        };
        var createResponse = await _client.PostAsJsonAsync("/api/academic-years", createRequest);
        Guid id = await GetIdFromResponse(createResponse);

        // Act
        var response = await _client.DeleteAsync($"/api/academic-years/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/academic-years/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
