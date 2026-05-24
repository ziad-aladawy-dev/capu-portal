using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CapitalUniversity.Contract.Tests._Infra;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// End-to-end performance benchmarks against the ACTUAL controller
/// endpoints used in production — full HTTP stack, real SQL Server in
/// Docker. Replaces the earlier Core.UniTests perf file that called EF
/// directly and skipped the controllers' CSV / Excel / per-row import
/// loops.
///
/// <para>
/// <b>What "actual" means here.</b> Every measurement is wall-clock from
/// the moment we issue an <see cref="HttpClient.GetAsync"/> /
/// <see cref="HttpClient.PostAsJsonAsync"/> to the moment the response
/// body finishes streaming. That covers:
/// <list type="bullet">
///   <item>Routing, model binding from query string / JSON body.</item>
///   <item>JWT auth middleware + permission policy provider.</item>
///   <item>The controllers' own code — including the
///   <c>request.PageSize = 100000</c> override on the export paths and
///   the per-row <c>foreach { await CreateAsync }</c> on bulk-import
///   (the M17 finding from the backend assessment).</item>
///   <item>Service / repository layers against real SQL Server.</item>
///   <item>JSON name deserialisation, CSV escape, <c>string.Join</c> /
///   <c>XLWorkbook</c> creation, UTF-8 encoding, <c>File()</c> result
///   writing back through Kestrel.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Why per-test factory.</b> The API's startup runs DataSeeder,
/// UniversityStructureSeeder, IdentitySeeder, and the manifest
/// synchroniser on every host start. Tests need each other's data
/// invisible (counting rows would otherwise spuriously fail), so each
/// <c>[SkippableFact]</c> spins its own factory with a fresh database
/// inside the shared container — ~10 – 20 s startup × N tests, paid once
/// per benchmark.
/// </para>
/// </summary>
public class ControllerBulkPerformanceTests : IAsyncLifetime
{
    /// <summary>The SLO upper bound documented in the product spec.</summary>
    private const int BulkExportSize = 5_000;

    /// <summary>
    /// Reduced N for the bulk-import benchmarks. The current
    /// <c>StaffController.BulkImport</c> / <c>StudentsController.BulkImport</c>
    /// loop per-item with no transaction (M17). 5 000 rows at ~30 – 50
    /// ms each through the full HTTP+service+EF stack would burn 2 – 4
    /// minutes per test. 500 rows demonstrates the linear cost without
    /// melting CI time budgets — the wall-clock per row is what we
    /// publish.
    /// </summary>
    private const int BulkImportSize = 500;

    /// <summary>
    /// Page-budget envelope. Every "good path" assertion sits at 15 s so
    /// a ≥ 5× regression breaks the build but normal CI variance does
    /// not.
    /// </summary>
    private static readonly TimeSpan GoodPathCeiling = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The seeded super-admin (from <c>DataSeeder.SeedStaffAsync</c>) —
    /// used to obtain a JWT for the secured endpoints. Even though the
    /// three target controllers carry no <c>[HasPermission]</c> attribute
    /// (C2 finding), they still require the fallback
    /// <c>RequireAuthenticatedUser</c> policy. Logging in as the seeded
    /// admin is the simplest way to satisfy that.
    /// </summary>
    private const string AdminNationalId = "29801011234567";
    private const string AdminPassword = "admin123";

    private readonly ITestOutputHelper _output;
    private readonly SqlServerWebApplicationFactory _factory = new();

    public ControllerBulkPerformanceTests(ITestOutputHelper output) { _output = output; }

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    // ============================================================
    // Students
    // ============================================================

    [SkippableFact]
    public async Task Students_ExportCsv_5000_RealEndpoint_StaysUnderBudget()
    {
        Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason);
        await SeedStudentsAsync(BulkExportSize);
        var client = await AuthenticatedClientAsync();

        var (response, elapsed, bytes) = await MeasureHttpAsync(() =>
            client.GetAsync("/api/students/export/csv"));

        ReportHttp("Students / GET /export/csv", BulkExportSize, elapsed, bytes);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        bytes.Should().BeGreaterThan(0, "CSV body must contain header + N rows");
        elapsed.Should().BeLessThan(GoodPathCeiling,
            "the production CSV exporter (request.PageSize = 100000 + string.Join loop) must clear 15 s for 5 000 rows");
    }

    [SkippableFact]
    public async Task Students_ExportExcel_5000_RealEndpoint_StaysUnderBudget()
    {
        Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason);
        await SeedStudentsAsync(BulkExportSize);
        var client = await AuthenticatedClientAsync();

        var (response, elapsed, bytes) = await MeasureHttpAsync(() =>
            client.GetAsync("/api/students/export-excel"));

        ReportHttp("Students / GET /export-excel", BulkExportSize, elapsed, bytes);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bytes.Should().BeGreaterThan(0);
        // ClosedXML's in-memory XLWorkbook is dramatically heavier than
        // CSV. Triple the CSV budget to keep noise out — still catches a
        // ≥ 3× regression on top of the natural Excel overhead.
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(45),
            "the production XLWorkbook + foreach exporter must clear 45 s for 5 000 rows");
    }

    [SkippableFact]
    public async Task Students_BulkImport_500_RealEndpoint_M17PerRowPattern_DocumentsCurrentCost()
    {
        Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason);
        // Documents the M17 cost. The endpoint loops `foreach (req in requests)
        // _service.CreateAsync(req)` with no batching — every row pays the
        // full service-layer + EF SaveChanges round-trip. We do NOT assert
        // an upper bound (the point is to publish the cost so the fix
        // lands), only that it returns 200 with the expected count.
        var structureNodeId = await GetSeededLevelNodeIdAsync();
        var client = await AuthenticatedClientAsync();
        var payload = Enumerable.Range(0, BulkImportSize)
            .Select(i => NewStudentRequest(structureNodeId, i))
            .ToList();

        var (response, elapsed, _) = await MeasureHttpAsync(() =>
            client.PostAsJsonAsync("/api/students/bulk-import", payload));

        ReportHttp("Students / POST /bulk-import (M17 per-row)", BulkImportSize, elapsed, payloadRowCount: BulkImportSize);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var inserted = await db.Students.AsNoTracking()
            .CountAsync(s => s.StudentCode.StartsWith("PERF-STU-"));
        inserted.Should().Be(BulkImportSize);
    }

    // ============================================================
    // Staff
    // ============================================================

    [SkippableFact]
    public async Task Staff_ExportCsv_5000_RealEndpoint_StaysUnderBudget()
    {
        Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason);
        await SeedStaffAsync(BulkExportSize);
        var client = await AuthenticatedClientAsync();

        var (response, elapsed, bytes) = await MeasureHttpAsync(() =>
            client.GetAsync("/api/staff/export/csv"));

        ReportHttp("Staff / GET /export/csv", BulkExportSize, elapsed, bytes);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        bytes.Should().BeGreaterThan(0);
        elapsed.Should().BeLessThan(GoodPathCeiling);
    }

    [SkippableFact]
    public async Task Staff_BulkImport_500_RealEndpoint_M17PerRowPattern_DocumentsCurrentCost()
    {
        Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason);
        var structureNodeId = await GetSeededLevelNodeIdAsync();
        var client = await AuthenticatedClientAsync();
        var payload = Enumerable.Range(0, BulkImportSize)
            .Select(i => NewStaffRequest(structureNodeId, i))
            .ToList();

        var (response, elapsed, _) = await MeasureHttpAsync(() =>
            client.PostAsJsonAsync("/api/staff/bulk-import", payload));

        ReportHttp("Staff / POST /bulk-import (M17 per-row)", BulkImportSize, elapsed, payloadRowCount: BulkImportSize);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var inserted = await db.Staffs.AsNoTracking()
            .CountAsync(s => s.EmployeeCode.StartsWith("PERF-STA-"));
        inserted.Should().Be(BulkImportSize);
    }

    // ============================================================
    // UniversityStructure
    // ============================================================

    [SkippableFact]
    public async Task UniversityStructure_GetTree_5000Nodes_RealEndpoint_StaysUnderBudget()
    {
        Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason);
        await SeedStructureNodesAsync(BulkExportSize);
        var client = await AuthenticatedClientAsync();

        var (response, elapsed, bytes) = await MeasureHttpAsync(() =>
            client.GetAsync("/api/university-structure/tree"));

        ReportHttp("UniversityStructure / GET /tree", BulkExportSize, elapsed, bytes);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bytes.Should().BeGreaterThan(0);
        // GetTreeAsync builds an in-memory tree from a flat-list query,
        // which is O(n) but the JSON serialization of a 5 000-node tree
        // is the dominant cost. 15 s is generous.
        elapsed.Should().BeLessThan(GoodPathCeiling,
            "the unbounded-depth /tree endpoint (M3 finding) must still clear 15 s at the 5 000-node SLO");
    }

    // ============================================================
    // Bench harness + helpers
    // ============================================================

    private static async Task<(HttpResponseMessage Response, TimeSpan Elapsed, long Bytes)> MeasureHttpAsync(
        Func<Task<HttpResponseMessage>> work)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();
        var response = await work();
        // Force the body to drain so wall-clock includes response streaming.
        var bytes = (long)(await response.Content.ReadAsByteArrayAsync()).Length;
        sw.Stop();
        return (response, sw.Elapsed, bytes);
    }

    private void ReportHttp(string label, int rowCount, TimeSpan elapsed, long? bytes = null, int? payloadRowCount = null)
    {
        var ms = elapsed.TotalMilliseconds;
        var rowsPerSec = ms > 0 ? rowCount / (ms / 1000d) : double.NaN;
        var msPerRow = rowCount > 0 ? ms / rowCount : double.NaN;
        var bytesLabel = bytes.HasValue ? $"body={bytes.Value / 1024d:F1} KB" : "body=n/a";
        _output.WriteLine(
            $"{label,-55} rows={rowCount,5}  elapsed={ms,8:F1} ms  throughput={rowsPerSec,7:F0} rows/s  avg={msPerRow,6:F2} ms/row  {bytesLabel}");
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        // Bulk-import on the M17 per-row path can take longer than the
        // default 100s HttpClient timeout. Set the longer timeout BEFORE
        // any request so HttpClient doesn't refuse the assignment with
        // "instance has already started one or more requests".
        client.Timeout = TimeSpan.FromMinutes(5);
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = AdminNationalId, Password = AdminPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            "the seeded super admin must be able to log in against the test SQL DB");
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private async Task<Guid> GetSeededLevelNodeIdAsync()
    {
        // UniversityStructureSeeder creates a hierarchy; we just need any
        // node whose Type==Level, which is what CreateStudentRequest /
        // CreateStaffRequest both expect.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var levelNode = await db.StructureNodes.AsNoTracking()
            .Where(n => n.Type == StructureNodeType.Level)
            .Select(n => n.Id)
            .FirstOrDefaultAsync();
        if (levelNode == default)
        {
            // Fallback: seed our own level node if the seeder didn't.
            var node = NewStructureNode(parentId: null, depth: 1, order: 0, type: StructureNodeType.Level);
            db.StructureNodes.Add(node);
            await db.SaveChangesAsync();
            return node.Id;
        }
        return levelNode;
    }

    private async Task SeedStudentsAsync(int count)
    {
        var structureNodeId = await GetSeededLevelNodeIdAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var rows = Enumerable.Range(0, count)
            .Select(i => new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = $"PERF-STU-{i:D8}",
                NationalId = $"PERF-NID-{i:D10}",
                Name = "{\"en\":\"T\"}",
                BirthDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PhoneNumber = "0100",
                Email = $"perf-stu-{i}-{Guid.NewGuid():N}@t.eg",
                StructureNodeId = structureNodeId,
                PasswordHash = "x",
                IsActive = true,
            })
            .ToList();
        db.Students.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private async Task SeedStaffAsync(int count)
    {
        var structureNodeId = await GetSeededLevelNodeIdAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var rows = Enumerable.Range(0, count)
            .Select(i => new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = $"PERF-STA-{i:D8}",
                NationalId = $"PERF-SNID-{i:D10}",
                Name = "{\"en\":\"T\"}",
                BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PhoneNumber = "0110",
                Email = $"perf-sta-{i}-{Guid.NewGuid():N}@t.eg",
                StructureNodeId = structureNodeId,
                PasswordHash = "x",
                IsActive = true,
            })
            .ToList();
        db.Staffs.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private async Task SeedStructureNodesAsync(int childCount)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        // Pick any seeded root; we'll attach all perf children under it
        // so the tree-export reflects the inflated count.
        var root = await db.StructureNodes.AsNoTracking()
            .Where(n => n.ParentId == null)
            .Select(n => n.Id)
            .FirstOrDefaultAsync();
        if (root == default)
        {
            var newRoot = NewStructureNode(parentId: null, depth: 1, order: 0, type: StructureNodeType.University);
            db.StructureNodes.Add(newRoot);
            await db.SaveChangesAsync();
            root = newRoot.Id;
        }
        var children = Enumerable.Range(0, childCount)
            .Select(i => NewStructureNode(parentId: root, depth: 2, order: i, type: StructureNodeType.Level))
            .ToList();
        db.StructureNodes.AddRange(children);
        await db.SaveChangesAsync();
    }

    private static StructureNode NewStructureNode(Guid? parentId, int depth, int order, StructureNodeType type)
    {
        var id = Guid.NewGuid();
        return new StructureNode
        {
            Id = id,
            Name = "{\"en\":\"PerfNode\"}",
            Type = type,
            ParentId = parentId,
            Path = "/" + id.ToString("N"),
            Depth = depth,
            Order = order,
            IsActive = true,
        };
    }

    private static CreateStudentRequest NewStudentRequest(Guid structureNodeId, int index) => new()
    {
        StudentCode = $"PERF-STU-{index:D8}",
        Password = "Password1!",
        ConfirmPassword = "Password1!",
        Name = "{\"en\":\"T\"}",
        NationalId = $"PERF-NID-{index:D10}",
        BirthDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        PhoneNumber = "0100",
        Email = $"perf-stu-{index}-{Guid.NewGuid():N}@t.eg",
        StructureNodeId = structureNodeId,
    };

    private static CreateStaffRequest NewStaffRequest(Guid structureNodeId, int index) => new()
    {
        EmployeeCode = $"PERF-STA-{index:D8}",
        Password = "Password1!",
        ConfirmPassword = "Password1!",
        Name = "{\"en\":\"T\"}",
        NationalId = $"PERF-SNID-{index:D10}",
        BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        PhoneNumber = "0110",
        Email = $"perf-sta-{index}-{Guid.NewGuid():N}@t.eg",
        Role = "Lecturer",
        JobTitle = "Lecturer",
        StructureNodeId = structureNodeId,
    };
}
