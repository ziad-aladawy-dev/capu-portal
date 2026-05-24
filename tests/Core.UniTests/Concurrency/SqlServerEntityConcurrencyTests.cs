using System.Collections.Concurrent;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.UniTests.Concurrency._Infra;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Concurrency;

/// <summary>
/// Real SQL Server concurrency tests for the three controllers that the
/// existing <see cref="SqlServerConcurrencyTests"/> didn't cover: Student,
/// Staff, and UniversityStructure. Same fixture, same isolation pattern —
/// per-test database inside the shared Docker container.
///
/// <para>
/// <b>What changes per entity.</b>
/// Student and Staff each carry two unique indexes — <c>StudentCode</c> /
/// <c>EmployeeCode</c> and <c>NationalId</c> — that must reject duplicates
/// under parallel insert. InMemory cannot observe either rejection.
/// UniversityStructure has no unique constraints by design (siblings may
/// share an <c>Order</c>); the tests here pin that contract by asserting
/// concurrent sibling inserts all persist, AND that the soft-delete
/// global query filter is honoured by real SQL reads.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public class SqlServerEntityConcurrencyTests : IAsyncLifetime
{
    private readonly SqlServerDbFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    // SQL Server unique-violation error numbers. Same constants the
    // production GlobalExceptionHandler uses to map DbUpdateException → 409.
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql &&
            (sql.Number == SqlUniqueConstraintViolation ||
             sql.Number == SqlUniqueIndexViolation);

    // ============================================================
    // Student — unique-index races
    // ============================================================

    [SkippableFact]
    public async Task CreateStudent_ParallelSameNationalId_UniqueIndexAllowsOnlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Invariant: the unique index on Students.NationalId rejects all but
        // one of N parallel inserts that share a NationalId. This is the
        // schema-level guarantee that the H6 student-code race fix relies on
        // — InMemory would happily admit duplicates and mask the bug.
        const int parallelism = 6;
        const string sharedNationalId = "29001010000001";
        var structureNodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            var s = NewStudent(structureNodeId,
                studentCode: "S-" + i.ToString("D6") + "-" + Guid.NewGuid().ToString("N")[..6],
                nationalId: sharedNationalId);
            db.Students.Add(s);
            try { await db.SaveChangesAsync(); return "ok"; }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return "rejected"; }
        });

        outcomes.Count(o => o == "ok").Should().Be(1);
        outcomes.Count(o => o == "rejected").Should().Be(parallelism - 1);

        await using var verify = _fixture.NewContext();
        var rows = await verify.Students.AsNoTracking()
            .Where(s => s.NationalId == sharedNationalId)
            .ToListAsync();
        rows.Should().HaveCount(1);
    }

    [SkippableFact]
    public async Task CreateStudent_ParallelSameStudentCode_UniqueIndexAllowsOnlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Invariant: the unique index on Students.StudentCode catches the
        // dominant race in the production "generate next student code" path
        // (H6). If two concurrent CreateAsync calls each compute the same
        // next code, the second SaveChanges MUST fail with a unique
        // violation — never a silent duplicate.
        const int parallelism = 6;
        const string sharedCode = "S-CODE-FIXED-001";
        var structureNodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            var s = NewStudent(structureNodeId,
                studentCode: sharedCode,
                nationalId: Guid.NewGuid().ToString("N")[..14] + i);
            db.Students.Add(s);
            try { await db.SaveChangesAsync(); return "ok"; }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return "rejected"; }
        });

        outcomes.Count(o => o == "ok").Should().Be(1);
        outcomes.Count(o => o == "rejected").Should().Be(parallelism - 1);

        await using var verify = _fixture.NewContext();
        var rows = await verify.Students.AsNoTracking()
            .Where(s => s.StudentCode == sharedCode)
            .ToListAsync();
        rows.Should().HaveCount(1);
    }

    [SkippableFact]
    public async Task CreateStudent_ParallelDistinctIdentifiers_AllPersist()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Companion test: pin that the unique indexes are NOT broader than
        // declared. If a future migration accidentally added (e.g.) Email
        // to the unique-key surface, this test would flip.
        const int parallelism = 6;
        var structureNodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            db.Students.Add(NewStudent(structureNodeId,
                studentCode: "S-DISTINCT-" + i,
                nationalId: "29001010000" + i.ToString("D3"),
                email: "shared-email@t.eg")); // SAME email — must not block
            await db.SaveChangesAsync();
            return "ok";
        });
        outcomes.Should().AllSatisfy(o => o.Should().Be("ok"));

        await using var verify = _fixture.NewContext();
        (await verify.Students.AsNoTracking().CountAsync()).Should().Be(parallelism);
    }

    // ============================================================
    // Staff — unique-index races
    // ============================================================

    [SkippableFact]
    public async Task CreateStaff_ParallelSameEmployeeCode_UniqueIndexAllowsOnlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        const int parallelism = 6;
        const string sharedCode = "E-FIXED-001";
        var structureNodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            var staff = NewStaff(structureNodeId,
                employeeCode: sharedCode,
                nationalId: Guid.NewGuid().ToString("N")[..14] + i);
            db.Staffs.Add(staff);
            try { await db.SaveChangesAsync(); return "ok"; }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return "rejected"; }
        });

        outcomes.Count(o => o == "ok").Should().Be(1);
        outcomes.Count(o => o == "rejected").Should().Be(parallelism - 1);

        await using var verify = _fixture.NewContext();
        (await verify.Staffs.AsNoTracking()
            .CountAsync(s => s.EmployeeCode == sharedCode)).Should().Be(1);
    }

    [SkippableFact]
    public async Task CreateStaff_ParallelSameNationalId_UniqueIndexAllowsOnlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        const int parallelism = 6;
        const string sharedNationalId = "29001010000005";
        var structureNodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            db.Staffs.Add(NewStaff(structureNodeId,
                employeeCode: "E-" + i.ToString("D6") + "-" + Guid.NewGuid().ToString("N")[..6],
                nationalId: sharedNationalId));
            try { await db.SaveChangesAsync(); return "ok"; }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return "rejected"; }
        });

        outcomes.Count(o => o == "ok").Should().Be(1);
        outcomes.Count(o => o == "rejected").Should().Be(parallelism - 1);
    }

    [SkippableFact]
    public async Task CreateStaff_ParallelDistinctIdentifiers_AllPersist()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        const int parallelism = 6;
        var structureNodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            db.Staffs.Add(NewStaff(structureNodeId,
                employeeCode: "E-DIST-" + i,
                nationalId: "29001020000" + i.ToString("D3"),
                email: "shared-staff-email@t.eg"));
            await db.SaveChangesAsync();
            return "ok";
        });
        outcomes.Should().AllSatisfy(o => o.Should().Be("ok"));

        await using var verify = _fixture.NewContext();
        (await verify.Staffs.AsNoTracking().CountAsync()).Should().Be(parallelism);
    }

    // ============================================================
    // UniversityStructure — contract pins (NO unique on (ParentId, Order),
    // soft-delete global query filter enforced server-side)
    // ============================================================

    [SkippableFact]
    public async Task CreateChildren_ParallelSiblingsSameOrder_NoUniqueOnOrder_AllPersist()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Contract pin: the schema intentionally does NOT carry a unique
        // index on (ParentId, Order). The structure service tolerates
        // duplicate Order values among siblings (a UI reorder operation
        // resolves them later). If a future migration accidentally added a
        // unique constraint, this test flips and the team has a clear
        // contract conversation.
        const int parallelism = 6;
        var parentId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(parallelism, async i =>
        {
            await using var db = _fixture.NewContext();
            var child = NewStructureNode(parentId, depth: 2, order: 0);
            db.StructureNodes.Add(child);
            await db.SaveChangesAsync();
            return "ok";
        });
        outcomes.Should().AllSatisfy(o => o.Should().Be("ok"));

        await using var verify = _fixture.NewContext();
        var siblings = await verify.StructureNodes.AsNoTracking()
            .Where(n => n.ParentId == parentId).ToListAsync();
        siblings.Should().HaveCount(parallelism);
        siblings.Select(s => s.Order).Should().AllBeEquivalentTo(0,
            "(ParentId, Order) is intentionally non-unique — concurrent siblings persist with duplicate Order");
    }

    [SkippableFact]
    public async Task SoftDelete_QueryFilter_ExcludesDeletedRowsFromReads_RealSql()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // EF query filter `HasQueryFilter(x => !x.IsDeleted)` lives in
        // CoreDbContext.OnModelCreating. This test confirms that real SQL
        // reads honour the filter — soft-deleted rows are invisible to
        // standard queries, only surfaced via `IgnoreQueryFilters()`. A
        // mutation that drops or weakens the filter (e.g. swaps the boolean
        // sense) would leak deleted rows to callers.
        var keptId = await SeedStructureNodeAsync();
        var deletedId = await SeedStructureNodeAsync();

        await using (var mutate = _fixture.NewContext())
        {
            var deleted = await mutate.StructureNodes.FirstAsync(n => n.Id == deletedId);
            deleted.IsDeleted = true;
            await mutate.SaveChangesAsync();
        }

        await using var verify = _fixture.NewContext();
        var visibleIds = await verify.StructureNodes.AsNoTracking()
            .Select(n => n.Id).ToListAsync();
        visibleIds.Should().Contain(keptId);
        visibleIds.Should().NotContain(deletedId,
            "the global query filter must hide soft-deleted rows from default reads");

        var allWithFilterOff = await verify.StructureNodes.AsNoTracking()
            .IgnoreQueryFilters().Select(n => n.Id).ToListAsync();
        allWithFilterOff.Should().Contain(new[] { keptId, deletedId },
            "IgnoreQueryFilters() must still surface the deleted row — the filter is a default, not an erasure");
    }

    [SkippableFact]
    public async Task ConcurrentSoftDelete_SameNode_BothCommitsConverge_NoExceptionAndRowIsDeleted()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // StructureNode carries NO RowVersion — concurrency control is
        // last-write-wins. Two writers both flipping IsDeleted=true on the
        // same row MUST both succeed and the final state must be deleted.
        // Documents the absence of a concurrency token at this layer; a
        // future migration that adds one would flip this test as a signal.
        //
        // Earlier version of this test used `ManualResetEventSlim.Wait()`
        // inside two async tasks — that blocks the underlying thread-pool
        // thread synchronously, and with both tasks blocked + the SQL
        // connection pool busy, the test host process repeatedly
        // crashed under Testcontainers. Switching to async jitter (the
        // same pattern the unique-violation tests use) creates the same
        // race window without thread-pool starvation.
        var nodeId = await SeedStructureNodeAsync();

        var outcomes = await RunInParallelAsync(2, async _ =>
        {
            await using var db = _fixture.NewContext();
            var node = await db.StructureNodes.FirstAsync(n => n.Id == nodeId);
            node.IsDeleted = true;
            await db.SaveChangesAsync();
            return "ok";
        });
        outcomes.Should().AllSatisfy(o => o.Should().Be("ok"),
            "no concurrency token on StructureNode — both soft-deletes must succeed (last-write-wins)");

        await using var verify = _fixture.NewContext();
        var deleted = await verify.StructureNodes.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstAsync(n => n.Id == nodeId);
        deleted.IsDeleted.Should().BeTrue();
    }

    // ============================================================
    // Helpers — kept inline so a future move of the existing seed
    // helpers in SqlServerConcurrencyTests doesn't ripple here.
    // ============================================================

    private static async Task<T[]> RunInParallelAsync<T>(int parallelism, Func<int, Task<T>> work)
    {
        var bag = new ConcurrentBag<T>();
        var tasks = Enumerable.Range(0, parallelism).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(0, 5));
            bag.Add(await work(i));
        }).ToArray();
        await Task.WhenAll(tasks);
        return bag.ToArray();
    }

    private async Task<Guid> SeedStructureNodeAsync()
    {
        await using var db = _fixture.NewContext();
        var node = NewStructureNode(parentId: null, depth: 1, order: 0);
        db.StructureNodes.Add(node);
        await db.SaveChangesAsync();
        return node.Id;
    }

    private static StructureNode NewStructureNode(Guid? parentId, int depth, int order)
    {
        var id = Guid.NewGuid();
        return new StructureNode
        {
            Id = id,
            Name = "{\"en\":\"N\"}",
            Type = StructureNodeType.Level,
            ParentId = parentId,
            Path = "/" + id.ToString("N"),
            Depth = depth,
            Order = order,
            IsActive = true,
        };
    }

    private static Student NewStudent(Guid structureNodeId, string studentCode, string nationalId, string? email = null) => new()
    {
        Id = Guid.NewGuid(),
        StudentCode = studentCode,
        NationalId = nationalId,
        Name = "{\"en\":\"T\"}",
        BirthDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        PhoneNumber = "01000000000",
        Email = email ?? (Guid.NewGuid().ToString("N") + "@t.eg"),
        StructureNodeId = structureNodeId,
        PasswordHash = "x",
        IsActive = true,
    };

    private static Staff NewStaff(Guid structureNodeId, string employeeCode, string nationalId, string? email = null) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeCode = employeeCode,
        NationalId = nationalId,
        Name = "{\"en\":\"T\"}",
        BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        PhoneNumber = "01100000000",
        Email = email ?? (Guid.NewGuid().ToString("N") + "@t.eg"),
        StructureNodeId = structureNodeId,
        PasswordHash = "x",
        IsActive = true,
    };
}
