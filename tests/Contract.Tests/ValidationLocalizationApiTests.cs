using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// End-to-end verification of the Runtime Hardening Plan §1 and §2 fixes:
/// MVC no longer short-circuits invalid requests (services produce the response)
/// and validation responses carry localized titles/details/error arrays.
/// </summary>
public class ValidationLocalizationApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;
    private readonly HttpClient _client;

    public ValidationLocalizationApiTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "LocValidationDb_" + Guid.NewGuid();
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
        var module = new Module { Id = Guid.NewGuid(), DisplayName = "Academic Timeline", ModuleKey = "academics" };
        var resource = new Resource { Id = Guid.NewGuid(), Module = module, ModuleId = module.Id, Key = "academic-years", DisplayName = "Academic Timeline" };
        var role = new Role { Id = Guid.NewGuid(), Name = "AdminRole" };

        db.Modules.Add(module);
        db.Resources.Add(resource);
        db.Roles.Add(role);
        db.AddCrudGrant(role.Id, resource.Id, ActionLevel.Delete);
        db.StaffRoles.Add(new StaffRoleAssignment(userId, role.Id, "Global", "Global"));

        var anyNodeId = db.StructureNodes.AsNoTracking().Select(n => n.Id).First();
        db.Staffs.Add(new Staff
        {
            Id = userId,
            EmployeeCode = string.Concat("LOC-", userId.ToString("N").AsSpan(0, 8)),
            NationalId = string.Concat("LOC", userId.ToString("N").AsSpan(0, 11)),
            PasswordHash = "test-hash",
            Name = "Loc Admin",
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

    [Fact]
    public async Task EndBeforeStart_ReturnsBadRequest_WithLocalizedTitleAndDetail_English()
    {
        _client.DefaultRequestHeaders.Remove("Accept-Language");
        _client.DefaultRequestHeaders.Add("Accept-Language", "en");

        var request = new CreateAcademicYearRequest
        {
            Name = "Bad Year",
            StartDate = new DateTime(2030, 1, 1),
            EndDate = new DateTime(2029, 1, 1),
        };

        var response = await _client.PostAsJsonAsync("/api/academic-years", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "§1.1 ensures the service runs and produces a localized ProblemDetails instead of MVC's raw 400");

        var problem = await ParseProblem(response);
        problem.GetProperty("title").GetString().Should().NotBeNullOrEmpty();

        // Errors array should contain the localized message for EndAfterStart, not the key.
        problem.GetProperty("errors").ToString()
            .Should().Contain(LocalizedStrings.Resolve(LocalizedKeys.Semesters.EndAfterStart, "en"));
    }

    [Fact]
    public async Task EndBeforeStart_Arabic_ResponseDetailsAreArabic()
    {
        _client.DefaultRequestHeaders.Remove("Accept-Language");
        _client.DefaultRequestHeaders.Add("Accept-Language", "ar");

        var request = new CreateAcademicYearRequest
        {
            Name = "Bad Year",
            StartDate = new DateTime(2031, 1, 1),
            EndDate = new DateTime(2030, 1, 1),
        };

        var response = await _client.PostAsJsonAsync("/api/academic-years", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ParseProblem(response);

        // Title must be Arabic.
        var arabicTitle = LocalizedStrings.Resolve(LocalizedKeys.Infrastructure.ValidationError, "ar");
        problem.GetProperty("title").GetString().Should().Be(arabicTitle);

        // Validation array's payload must be Arabic too — §2.4
        var errorsJson = problem.GetProperty("errors").ToString();
        var arabicEndAfterStart = LocalizedStrings.Resolve(LocalizedKeys.Semesters.EndAfterStart, "ar");
        errorsJson.Should().Contain(arabicEndAfterStart);
    }

    [Fact]
    public async Task TraceIdAlwaysPresent_OnValidationFailure()
    {
        _client.DefaultRequestHeaders.Remove("Accept-Language");

        var request = new CreateAcademicYearRequest
        {
            Name = "Bad Year",
            StartDate = new DateTime(2032, 1, 1),
            EndDate = new DateTime(2031, 1, 1),
        };

        var response = await _client.PostAsJsonAsync("/api/academic-years", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ParseProblem(response);
        problem.TryGetProperty("traceId", out var trace).Should().BeTrue();
        trace.GetString().Should().NotBeNullOrEmpty();
    }

    private static async Task<JsonElement> ParseProblem(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
