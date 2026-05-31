using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// P1.1 integration coverage. Verifies that:
///   <list type="bullet">
///     <item>Out-of-scope GET returns 404 (no existence leak vs forbidden).</item>
///     <item>The shared object cache cannot be used to bypass scope —
///       a user whose query warmed the cache cannot grant a different
///       out-of-scope user access.</item>
///     <item>An in-scope user still gets the cached payload on the second hit.</item>
///   </list>
/// </summary>
public class ScopeEnforcementTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;
    private readonly HttpClient _client;

    // Fixed ids so the seed + per-test setup line up.
    private readonly Guid _facultyAId = Guid.NewGuid();
    private readonly Guid _facultyBId = Guid.NewGuid();
    private readonly Guid _studentAId = Guid.NewGuid();
    private readonly Guid _studentBId = Guid.NewGuid();
    private readonly Guid _staffAId = Guid.NewGuid();
    private readonly Guid _staffBId = Guid.NewGuid();
    private readonly Guid _invoiceForAId = Guid.NewGuid();

    public ScopeEnforcementTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "ScopeTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(opts => opts.UseInMemoryDatabase(dbName));

                services.RemoveAll<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>();
                services.AddScoped(_ => Mock.Of<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>());
            });
        });

        _client = _factory.CreateClient();
        Seed();
    }

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        // Two separate sibling faculty subtrees so neither path is a prefix of
        // the other — keeps the scope check honest.
        var faculties = new[]
        {
            new StructureNode { Id = _facultyAId, Name = "Faculty A", Type = StructureNodeType.Faculty, Path = "/faculty-a", Depth = 1 },
            new StructureNode { Id = _facultyBId, Name = "Faculty B", Type = StructureNodeType.Faculty, Path = "/faculty-b", Depth = 1 },
        };
        db.StructureNodes.AddRange(faculties);

        // Module + Resource + Role + role-permission for invoice view. Path scope
        // applied per-staff via StaffRoleAssignment.StructureNodeId/Path below.
        var module = new Module { Id = Guid.NewGuid(), DisplayName = "Payments", ModuleKey = "payments" };
        var resource = new Resource { Id = Guid.NewGuid(), Module = module, ModuleId = module.Id, Key = "invoices", DisplayName = "Invoices" };
        var role = new Role { Id = Guid.NewGuid(), Name = "InvoiceViewer" };
        db.Modules.Add(module);
        db.Resources.Add(resource);
        db.Roles.Add(role);
        db.AddCrudGrant(role.Id, resource.Id, ActionLevel.Delete);

        // Two staff, each scoped to a single faculty subtree.
        db.Staffs.AddRange(
            new Staff { Id = _staffAId, EmployeeCode = "SA", NationalId = "NA-A", PasswordHash = "x", Name = "Staff A", Role = "InvoiceViewer", StructureNodeId = _facultyAId, IsActive = true },
            new Staff { Id = _staffBId, EmployeeCode = "SB", NationalId = "NA-B", PasswordHash = "x", Name = "Staff B", Role = "InvoiceViewer", StructureNodeId = _facultyBId, IsActive = true });

        db.StaffRoles.AddRange(
            new StaffRoleAssignment(_staffAId, role.Id, "Global", "Global") { StructureNodeId = _facultyAId, StructureNodePath = "/faculty-a" },
            new StaffRoleAssignment(_staffBId, role.Id, "Global", "Global") { StructureNodeId = _facultyBId, StructureNodePath = "/faculty-b" });

        // Two students, one in each faculty.
        db.Students.AddRange(
            new Student { Id = _studentAId, StudentCode = "STU-A", NationalId = "SA-A", PasswordHash = "x", Name = "Student A", Email = "a@x", StructureNodeId = _facultyAId, IsActive = true },
            new Student { Id = _studentBId, StudentCode = "STU-B", NationalId = "SA-B", PasswordHash = "x", Name = "Student B", Email = "b@x", StructureNodeId = _facultyBId, IsActive = true });

        // Invoice belongs to Student A (i.e. lives in Faculty A's subtree).
        db.Set<Invoice>().Add(new Invoice
        {
            Id = _invoiceForAId,
            StudentId = _studentAId,
            Status = InvoiceStatus.Pending,
            TotalAmount = 100m,
            Currency = "EGP",
        });

        db.SaveChanges();
    }

    private void AuthenticateAs(Guid userId, string name, Guid structureNodeId)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var mockUser = new Mock<IUserCredential>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Identifier).Returns(name);
        mockUser.Setup(u => u.Role).Returns("InvoiceViewer");
        mockUser.Setup(u => u.Name).Returns(name);
        var token = tokens.GenerateToken(mockUser.Object);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // RequestContext reads scope from X-StructureNode-Id; without it the
        // permission service's path-prefix filter excludes a scoped role and
        // the call would 403 before IEffectiveScope ever runs. Staff carry
        // their own faculty id as the request scope.
        _client.DefaultRequestHeaders.Remove("X-StructureNode-Id");
        _client.DefaultRequestHeaders.Add("X-StructureNode-Id", structureNodeId.ToString());
    }

    [Fact]
    public async Task GetById_InScope_Returns200()
    {
        AuthenticateAs(_staffAId, "Staff A", _facultyAId);

        var response = await _client.GetAsync($"/api/invoices/{_invoiceForAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        body.Should().NotBeNull();
        body!.StudentId.Should().Be(_studentAId);
    }

    [Fact]
    public async Task GetById_OutOfScope_Returns404()
    {
        // Staff B is scoped to /faculty-b; the invoice lives in /faculty-a.
        AuthenticateAs(_staffBId, "Staff B", _facultyBId);

        var response = await _client.GetAsync($"/api/invoices/{_invoiceForAId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "out-of-scope rows must return 404 to avoid existence leakage");
    }

    [Fact]
    public async Task GetById_OutOfScope_RemainsHiddenEvenAfterInScopeWarmsCache()
    {
        // Staff A loads the invoice — populates the shared object cache.
        AuthenticateAs(_staffAId, "Staff A", _facultyAId);
        var warm = await _client.GetAsync($"/api/invoices/{_invoiceForAId}");
        warm.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm the cache was populated.
        using (var scope = _factory.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var cached = await cache.GetAsync<InvoiceResponse>($"invoice:object:{_invoiceForAId:N}");
            cached.Should().NotBeNull("warming call must populate the shared cache");
        }

        // Staff B tries to GET the same id — should still see 404.
        AuthenticateAs(_staffBId, "Staff B", _facultyBId);
        var leakAttempt = await _client.GetAsync($"/api/invoices/{_invoiceForAId}");

        leakAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "scope must be enforced on every cache hit, not just on database loads");
    }

    [Fact]
    public async Task GetById_InScope_HitsCacheOnSecondCall()
    {
        AuthenticateAs(_staffAId, "Staff A", _facultyAId);

        var first = await _client.GetAsync($"/api/invoices/{_invoiceForAId}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.GetAsync($"/api/invoices/{_invoiceForAId}");
        second.StatusCode.Should().Be(HttpStatusCode.OK,
            "in-scope users must continue to see the cached payload");

        var body = await second.Content.ReadFromJsonAsync<InvoiceResponse>();
        body!.Id.Should().Be(_invoiceForAId);
    }

    [Fact]
    public async Task GetForStudent_OutOfScope_ReturnsEmptyList()
    {
        // Staff B asking for Student A's invoices — empty list, no 403.
        AuthenticateAs(_staffBId, "Staff B", _facultyBId);

        var response = await _client.GetAsync($"/api/invoices/by-student/{_studentAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InvoiceResponse[]>();
        body.Should().NotBeNull().And.BeEmpty();
    }
}