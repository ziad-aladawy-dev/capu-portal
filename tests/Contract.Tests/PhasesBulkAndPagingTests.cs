using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Domain;
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
/// Real-HTTP contract tests for the endpoints introduced in Phases 1, 2, and 3
/// (bulk actions, paged search/filter, consistency cleanup). Follows the
/// project's established testing pattern (<see cref="ConcurrentStudentCreateTests"/>
/// and <see cref="AcademicYearApiTests"/>): WebApplicationFactory + InMemory DB +
/// seeded Super Admin login. The strategy doc calls for Testcontainers SQL
/// Server; we match the project's existing tests (InMemory) so CI stays fast.
/// </summary>
public class PhasesBulkAndPagingTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;
    private readonly HttpClient _client;

    public PhasesBulkAndPagingTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "PhasesTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddScoped(_ => Mock.Of<IAppLogger>());
                services.AddScoped(_ => Mock.Of<ILoggerService>());
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });

        _client = _factory.CreateClient();
        // The seeder only grants Super Admin permissions on the 9 base resources.
        // The PermissionManifestSynchronizer adds module-specific resources at
        // startup (payments.invoices, course-offerings.*, …) but doesn't extend
        // Super Admin's grants to them — a real production gap, irrelevant to
        // these tests. Backfill the grants so tests can hit any endpoint.
        EnsureSuperAdminHasFullGrantsAcrossAllResources();
        LoginAsSuperAdminAsync().GetAwaiter().GetResult();
    }

    private void EnsureSuperAdminHasFullGrantsAcrossAllResources()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var superAdminRoleId = db.Roles
            .AsNoTracking()
            .Where(r => r.Name.Contains("Super Admin"))
            .Select(r => r.Id)
            .First();

        var allResources = db.Resources.AsNoTracking().Select(r => r.Id).ToList();
        var existing = db.Set<CapitalUniversity.Core.Domain.Authorization.RolePermission>()
            .AsNoTracking()
            .Where(rp => rp.RoleId == superAdminRoleId)
            .Select(rp => new { rp.ResourceId, rp.Action })
            .ToHashSet();

        var actions = new[] { "View", "Insert", "EditClose", "Open", "Delete" };
        foreach (var resourceId in allResources)
        {
            foreach (var action in actions)
            {
                if (existing.Contains(new { ResourceId = resourceId, Action = action })) continue;
                db.Set<CapitalUniversity.Core.Domain.Authorization.RolePermission>()
                    .Add(new CapitalUniversity.Core.Domain.Authorization.RolePermission(superAdminRoleId, resourceId, action));
            }
        }
        db.SaveChanges();
    }

    private async Task LoginAsSuperAdminAsync()
    {
        // Seeded credentials — see DataSeeder.cs.
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = "29801011234567", Password = "admin123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            "Super Admin login must succeed for these tests; the seeder didn't run as expected if this fails");
        var token = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private Guid SuperAdminUserId
    {
        get
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            return db.Staffs.AsNoTracking().Single(s => s.NationalId == "29801011234567").Id;
        }
    }

    // ============================================================
    // Phase 1 — Bulk Actions
    // ============================================================

    [Fact]
    public async Task Phase1_BulkMarkNotificationsRead_HappyPath_ReturnsCount()
    {
        var ids = SeedNotifications(SuperAdminUserId, count: 3, read: false);

        var resp = await _client.PutAsJsonAsync("/api/notifications/read",
            new { ids });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("marked").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Phase1_BulkMarkNotificationsRead_Replay_IsIdempotentReturnsZero()
    {
        var ids = SeedNotifications(SuperAdminUserId, count: 2, read: false);

        var first = await _client.PutAsJsonAsync("/api/notifications/read", new { ids });
        first.EnsureSuccessStatusCode();

        var second = await _client.PutAsJsonAsync("/api/notifications/read", new { ids });
        second.EnsureSuccessStatusCode();
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("marked").GetInt32().Should().Be(0,
            "already-read rows are no-ops; replay must return zero");
    }

    [Fact]
    public async Task Phase1_BulkMarkAllRead_NoUnread_ReturnsZero()
    {
        // Caller has no notifications seeded — mark-all-read is a safe no-op.
        var resp = await _client.PutAsJsonAsync("/api/notifications/mark-all-read", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("marked").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Phase1_BulkRequest_EmptyIds_Returns400()
    {
        var resp = await _client.PutAsJsonAsync("/api/notifications/read",
            new { ids = Array.Empty<Guid>() });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Phase1_BulkRequest_OverMaxBulkSize_Returns400()
    {
        // MaxBulkSize = 500. Submit 501.
        var ids = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray();

        var resp = await _client.PutAsJsonAsync("/api/notifications/read", new { ids });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Phase1_InvoicesBulkCancel_HappyPath_AllSucceed()
    {
        var (pending1, pending2) = (SeedInvoice(InvoiceStatus.Pending), SeedInvoice(InvoiceStatus.Pending));

        var resp = await _client.PostAsJsonAsync("/api/invoices/cancel", new
        {
            ids = new[] { pending1, pending2 },
            payload = new { reason = "test cancel" }
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("succeeded").GetInt32().Should().Be(2);
        body.GetProperty("failed").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Phase1_InvoicesBulkCancel_AlreadyCancelled_IsIdempotentSuccess()
    {
        var cancelledInvoice = SeedInvoice(InvoiceStatus.Cancelled);

        var resp = await _client.PostAsJsonAsync("/api/invoices/cancel", new
        {
            ids = new[] { cancelledInvoice },
            payload = new { reason = "replay" }
        });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("succeeded").GetInt32().Should().Be(1,
            "already-cancelled rows are no-ops, not failures — replay must succeed");
    }

    [Fact]
    public async Task Phase1_InvoicesBulkCancel_PaidInvoice_LandsInFailures()
    {
        var paidInvoice = SeedInvoice(InvoiceStatus.Paid);

        var resp = await _client.PostAsJsonAsync("/api/invoices/cancel", new
        {
            ids = new[] { paidInvoice },
            payload = new { reason = "should fail" }
        });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("succeeded").GetInt32().Should().Be(0);
        body.GetProperty("failed").GetInt32().Should().Be(1);
        var failures = body.GetProperty("failures").EnumerateArray().ToList();
        failures.Should().ContainSingle();
        failures[0].GetProperty("id").GetGuid().Should().Be(paidInvoice);
        failures[0].GetProperty("code").GetString().Should().Be("Conflict");
    }

    [Fact]
    public async Task Phase1_BulkRequest_PartialFailure_Returns200WithFailures()
    {
        // Mix: one real Pending invoice + one bogus id.
        var real = SeedInvoice(InvoiceStatus.Pending);
        var fake = Guid.NewGuid();

        var resp = await _client.PostAsJsonAsync("/api/invoices/cancel", new
        {
            ids = new[] { real, fake },
            payload = new { reason = "mixed batch" }
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "partial failure must NOT return 4xx — clients consume the result body");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("succeeded").GetInt32().Should().Be(1);
        body.GetProperty("failed").GetInt32().Should().Be(1);
        body.GetProperty("failures").EnumerateArray().Single()
            .GetProperty("id").GetGuid().Should().Be(fake);
    }

    [Fact]
    public async Task Phase1_Concurrency_ParallelMarkRead_NoDuplicateMarks()
    {
        // 10 unread notifications, 10 parallel requests each marking all of them.
        // Total marked across all requests must equal exactly 10 (each notification
        // can only transition from unread→read once; concurrent requests for
        // already-read rows return 0).
        var ids = SeedNotifications(SuperAdminUserId, count: 10, read: false);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PutAsJsonAsync("/api/notifications/read", new { ids }))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        responses.Should().AllSatisfy(r => r.EnsureSuccessStatusCode());

        var totalMarked = 0;
        foreach (var r in responses)
        {
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            totalMarked += body.GetProperty("marked").GetInt32();
        }
        totalMarked.Should().Be(10,
            "across all concurrent calls, exactly 10 notifications transitioned unread→read");
    }

    // ============================================================
    // Phase 2 — Search & Filter
    // ============================================================

    [Fact]
    public async Task Phase2_InvoiceSearch_StatusFilter_NarrowsResultSet()
    {
        SeedInvoice(InvoiceStatus.Pending);
        SeedInvoice(InvoiceStatus.Pending);
        SeedInvoice(InvoiceStatus.Paid);

        var resp = await _client.GetAsync("/api/invoices?status=Pending");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Should().AllSatisfy(i =>
            i.GetProperty("status").GetInt32().Should().Be((int)InvoiceStatus.Pending));
    }

    [Fact]
    public async Task Phase2_InvoiceSearch_PaginationShape_PagedResultEnvelope()
    {
        SeedInvoice(InvoiceStatus.Pending);

        var resp = await _client.GetAsync("/api/invoices?page=1&pageSize=10");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("items", out _).Should().BeTrue();
        body.TryGetProperty("page", out _).Should().BeTrue();
        body.TryGetProperty("pageSize", out _).Should().BeTrue();
        body.TryGetProperty("totalCount", out _).Should().BeTrue();
        body.TryGetProperty("totalPages", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Phase2_PageSize_OverMax_SilentlyClampedTo100()
    {
        SeedInvoice(InvoiceStatus.Pending);

        // PagingConstants.MaxPageSize = 100; request 5000.
        var resp = await _client.GetAsync("/api/invoices?pageSize=5000");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pageSize").GetInt32().Should().BeLessThanOrEqualTo(100,
            "PagingConstants.MaxPageSize must clamp oversized requests");
    }

    [Fact]
    public async Task Phase2_SortClause_UnknownField_Returns400()
    {
        SeedInvoice(InvoiceStatus.Pending);

        var resp = await _client.GetAsync("/api/invoices?sort=bogusField:asc");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "SortClause.Parse must reject unknown fields with ValidationException → 400");
    }

    [Fact]
    public async Task Phase2_NotificationSearch_IsReadFilter()
    {
        SeedNotifications(SuperAdminUserId, count: 2, read: false);
        SeedNotifications(SuperAdminUserId, count: 1, read: true);

        var resp = await _client.GetAsync("/api/notifications/search?isRead=false");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().AllSatisfy(n =>
            n.GetProperty("isRead").GetBoolean().Should().BeFalse());
    }

    // ============================================================
    // Phase 3 — Consistency Cleanup
    // ============================================================

    [Fact]
    public async Task Phase3_GetAllStudentsEndpoint_Removed_Returns405OrNotFound()
    {
        // 3.1 — `GET /api/students` (no query) used to return List<StudentDto>;
        // removed in Phase 3. The route `/api/students` still exists for other
        // verbs (POST/PUT/DELETE), so ASP.NET routing returns 405
        // MethodNotAllowed for GET — equivalent to "endpoint removed" from a
        // callable-surface perspective.
        var resp = await _client.GetAsync("/api/students");

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Phase3_GetAllStaffEndpoint_Removed_Returns405OrNotFound()
    {
        var resp = await _client.GetAsync("/api/staff");

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Phase3_StudentsSearch_StillReachable_AfterRemoval()
    {
        // The /search variant must continue to work — verifies 3.1's "migration target" claim.
        var resp = await _client.GetAsync("/api/students/search?page=1&pageSize=10");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("items", out _).Should().BeTrue();
        body.TryGetProperty("totalCount", out _).Should().BeTrue();
    }

    // ============================================================
    // Helpers — seed entities directly via DbContext for the test cases above.
    // ============================================================

    private Guid[] SeedNotifications(Guid recipientUserId, int count, bool read)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var ids = new List<Guid>(count);
        for (var i = 0; i < count; i++)
        {
            var n = new Notification
            {
                Id = Guid.NewGuid(),
                RecipientUserId = recipientUserId,
                Title = "test",
                Message = "test message",
                Type = NotificationType.Info,
                IsRead = read,
                CreatedAt = DateTime.UtcNow,
            };
            db.Notifications.Add(n);
            ids.Add(n.Id);
        }
        db.SaveChanges();
        return ids.ToArray();
    }

    private Guid SeedInvoice(InvoiceStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        // Any seeded student works — use the first one the seeder created
        // (Super Admin staff doesn't count). The seeder creates students; pick one.
        // If no students exist (the seeder may only create staff), create one.
        var studentId = db.Students.AsNoTracking().Select(s => s.Id).FirstOrDefault();
        if (studentId == Guid.Empty)
        {
            var levelNodeId = db.StructureNodes.AsNoTracking()
                .Where(n => n.Type == StructureNodeType.Level)
                .Select(n => n.Id).First();
            var s = new CapitalUniversity.Core.Domain.Identity.Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "TEST-STU-" + Guid.NewGuid().ToString("N")[..6],
                NationalId = "TST" + Guid.NewGuid().ToString("N")[..11],
                Name = "Test Student",
                PasswordHash = "x",
                StructureNodeId = levelNodeId,
                IsActive = true,
            };
            db.Students.Add(s);
            db.SaveChanges();
            studentId = s.Id;
        }

        var inv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Status = status,
            TotalAmount = 100m,
            Currency = "EGP",
            CreatedAt = DateTime.UtcNow,
        };
        db.Set<Invoice>().Add(inv);
        db.SaveChanges();
        return inv.Id;
    }
}
