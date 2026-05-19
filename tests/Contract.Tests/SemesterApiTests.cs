using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
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

    // Seed the consolidated academic-timeline permission — one Service + one
    // RolePermission row covers BOTH academic-years and semesters controllers
    // via canonical "academics.academic-years.*".
    var module = new Module { Id = Guid.NewGuid(), DisplayName = "Academic Timeline", ModuleKey = "academics" };
    var service = new Service { Id = Guid.NewGuid(), ModuleId = module.Id, DisplayName = "Academic Timeline" };
    var role = new Role { Id = Guid.NewGuid(), Name = "AdminRole" };

    db.Modules.Add(module);
    db.Services.Add(service);
    db.Roles.Add(role);
    db.RolePermissions.Add(new RolePermission(role.Id, service.Id, "academic-years", ActionLevel.Delete));
    db.StaffRoles.Add(new StaffRoleAssignment(userId, role.Id, "Global", "Global"));

    // SessionVersionMiddleware needs a Staff row matching the token's user id.
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

    private async Task<Guid> GetIdFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_SemesterOutsideYearRange_ReturnsBadRequest()
    {
        // Year + semester windows pushed to 2030 so they don't overlap with
        // the 2023–2027 academic years DataSeeder ships in the test DB.
        // Arrange
        var yearRequest = new CreateAcademicYearRequest
        {
            Name = "Year",
            StartDate = new DateTime(2030, 9, 1),
            EndDate = new DateTime(2031, 6, 30)
        };
        var yearResp = await _client.PostAsJsonAsync("/api/academic-years", yearRequest);
        Guid yearId = await GetIdFromResponse(yearResp);

        var semesterRequest = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Invalid Semester",
            Order = 1,
            StartDate = new DateTime(2030, 8, 1), // Before year start
            EndDate = new DateTime(2030, 12, 31)
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
        // Use 2031 — outside DataSeeder's 2023–2027 seeded year range.
        // Arrange
        var yearRequest = new CreateAcademicYearRequest
        {
            Name = "Year 2031",
            StartDate = new DateTime(2031, 1, 1),
            EndDate = new DateTime(2031, 12, 31)
        };
        var yearResp = await _client.PostAsJsonAsync("/api/academic-years", yearRequest);
        Guid yearId = await GetIdFromResponse(yearResp);

        var sem1 = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Sem 1",
            Order = 1,
            StartDate = new DateTime(2031, 1, 1),
            EndDate = new DateTime(2031, 6, 30)
        };
        await _client.PostAsJsonAsync("/api/semesters", sem1);

        var sem2 = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Sem 2",
            Order = 2,
            StartDate = new DateTime(2031, 6, 1), // Overlap
            EndDate = new DateTime(2031, 12, 31)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/semesters", sem2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_SemesterDate_OutsideYearRange_ReturnsBadRequest()
    {
        // 2032 — clear of the seeded 2023–2027 range.
        // Arrange
        var yearRequest = new CreateAcademicYearRequest
        {
            Name = "Year 2032",
            StartDate = new DateTime(2032, 1, 1),
            EndDate = new DateTime(2032, 12, 31)
        };
        var yearResp = await _client.PostAsJsonAsync("/api/academic-years", yearRequest);
        Guid yearId = await GetIdFromResponse(yearResp);

        var semRequest = new CreateSemesterRequest
        {
            AcademicYearId = yearId,
            Name = "Sem 1",
            Order = 1,
            StartDate = new DateTime(2032, 1, 1),
            EndDate = new DateTime(2032, 6, 30)
        };
        var semResp = await _client.PostAsJsonAsync("/api/semesters", semRequest);
        Guid semId = await GetIdFromResponse(semResp);

        var updateRequest = new UpdateSemesterRequest
        {
            EndDate = new DateTime(2033, 1, 1) // Outside year
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/semesters/{semId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
