using System.Collections.Concurrent;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using CapitalUniversity.Core.UniTests.Concurrency._Infra;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Domain;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Core.UniTests.Concurrency;

/// <summary>
/// Real SQL Server concurrency tests. Replaces the InMemory-skipped /
/// InMemory-weakened scenarios in <see cref="WorstCaseTimingTests"/> with
/// versions that exercise the actual database behaviours those tests cannot
/// observe:
/// <list type="bullet">
///   <item>Unique constraint enforcement under parallel insert.</item>
///   <item><c>RowVersion</c> optimistic concurrency rejection (the
///   <see cref="DbUpdateConcurrencyException"/> path inside Try* repository
///   primitives).</item>
///   <item>Atomic <c>ExecuteUpdateAsync</c> under concurrent writers
///   (server-side row lock, lost-update prevention).</item>
///   <item>Idempotency-key unique index catching duplicate inserts.</item>
/// </list>
///
/// <para>
/// <b>Why real SQL.</b> EF Core's InMemory provider models a per-key
/// dictionary — it does not enforce unique indexes under parallel inserts,
/// does not honour the <c>RowVersion</c> concurrency token, and does not
/// translate <c>ExecuteUpdateAsync</c> into a server-side atomic update.
/// Any "passing" InMemory test of those behaviours mis-documents the
/// production guarantee. These tests run against a real SQL Server
/// instance (LocalDB by default, configurable via the
/// <c>CAPU_TEST_SQL_CONNECTION</c> env var) so a failure here means the
/// production schema or repository genuinely no longer holds the
/// invariant.
/// </para>
///
/// <para>
/// <b>Isolation strategy.</b> <see cref="SqlServerDbFixture"/> creates a
/// brand-new database for every test method (xUnit constructs the class
/// once per Fact) and drops it on disposal. No shared state, no
/// transaction-based isolation (which would hold locks and defeat the
/// concurrency tests). Every task in a parallel block uses its own
/// freshly constructed <see cref="CoreDbContext"/>; <c>DbContext</c> is
/// not thread-safe.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public class SqlServerConcurrencyTests : IAsyncLifetime
{
    private readonly SqlServerDbFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    // ---------------- ScheduleSlot — unique index under parallel insert ----------------

    [SkippableFact]
    public async Task AddExactDuplicateSlot_ParallelInsert_UniqueIndexRejectsAllButOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Invariant: SQL Server's unique index on
        // (CourseOfferingId, DayOfWeek, StartTime, EndTime) must allow at
        // most one row for the same tuple even when N writers race. The
        // losers must surface as DbUpdateException(InnerException.Number ==
        // 2627 or 2601). This was the test previously skipped on
        // InMemory — now exercised against real SQL.
        const int parallelism = 8;
        var offeringId = Guid.NewGuid();

        ScheduleSlot Build()
        {
            var s = new ScheduleSlot
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = offeringId,
                DayOfWeek = DayOfWeek.Monday,
                Kind = ScheduleSlotKind.Lecture,
            };
            s.SetTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 0));
            return s;
        }

        var outcomes = new ConcurrentBag<string>();
        var tasks = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await Task.Delay(Random.Shared.Next(0, 5));
            await using var db = _fixture.NewContext();
            try
            {
                db.Add(Build());
                await db.SaveChangesAsync();
                outcomes.Add("ok");
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                outcomes.Add("rejected");
            }
        }).ToArray();
        await Task.WhenAll(tasks);

        outcomes.Count(o => o == "ok").Should().Be(1,
            "the unique index allows exactly one row for the same (CourseOfferingId, DayOfWeek, StartTime, EndTime) tuple");
        outcomes.Count(o => o == "rejected").Should().Be(parallelism - 1,
            "every other writer must surface a unique-violation");

        await using var verify = _fixture.NewContext();
        var slots = await verify.Set<ScheduleSlot>()
            .AsNoTracking()
            .Where(s => s.CourseOfferingId == offeringId)
            .ToListAsync();
        slots.Should().HaveCount(1,
            "the database must end up with exactly one row for the duplicated tuple");
    }

    // ---------------- CourseOffering — RowVersion capacity invariant ----------------

    [SkippableFact]
    public async Task TryIncrementRegistration_50ParallelOnRealSql_ExactlyCapacitySucceed_RowVersionEnforced()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Invariant: under N concurrent registration attempts against an
        // offering with capacity C (N > C), exactly C return true AND the
        // persisted RegisteredCount equals C. The repository's retry loop
        // relies on SQL Server's RowVersion token to reject lost updates
        // with DbUpdateConcurrencyException, then re-reads and re-evaluates
        // the capacity guard. InMemory does not enforce RowVersion, so this
        // is the strictly-stronger SQL-only version of the multi-context
        // test in WorstCaseTimingTests.
        const int parallelism = 50;
        const int capacity = 10;

        var (courseId, semesterId, structureNodeId) = await SeedCourseSemesterNodeAsync();
        var offering = await SeedOfferingAsync(courseId, semesterId, structureNodeId, capacity);

        var successes = new ConcurrentBag<bool>();
        var tasks = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await Task.Delay(Random.Shared.Next(0, 5));
            await using var db = _fixture.NewContext();
            var repo = new CourseOfferingRepository(db);
            successes.Add(await repo.TryIncrementRegistrationAsync(offering.Id));
        }).ToArray();
        await Task.WhenAll(tasks);

        successes.Count(s => s).Should().Be(capacity,
            "exactly capacity attempts must succeed across {0} parallel registrations under RowVersion enforcement",
            parallelism);

        await using var verify = _fixture.NewContext();
        var reloaded = await verify.Set<CourseOfferingEntity>()
            .AsNoTracking()
            .FirstAsync(o => o.Id == offering.Id);
        reloaded.RegisteredCount.Should().Be(capacity,
            "no over-increment is allowed — SQL Server's RowVersion + the repository's bounded retry loop must serialise effectively at the capacity boundary");
        reloaded.RegisteredCount.Should().BeLessThanOrEqualTo(reloaded.Capacity);
    }

    // ---------------- PaymentTransaction — idempotency-key unique index ----------------

    [SkippableFact]
    public async Task RecordPayment_ParallelInsertSameIdempotencyKey_UniqueIndexAllowsOnlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Invariant: the unique index on PaymentTransactions
        // (InvoiceId, IdempotencyKey) must allow at most one row for the
        // same (invoice, key) pair, even when N webhooks fire concurrently.
        // The repository's SaveTransactionWithIdempotencyAsync catches the
        // unique-violation and treats it as a "replay" — exercising that
        // catch-block requires real SQL because InMemory would just write
        // both rows.
        var (courseId, semesterId, structureNodeId) = await SeedCourseSemesterNodeAsync();
        var studentId = await SeedStudentAsync(structureNodeId);
        var invoiceId = await SeedInvoiceAsync(studentId, totalAmount: 100m);

        const int parallelism = 6;
        const string sharedKey = "webhook-1";
        var outcomes = new ConcurrentBag<string>();

        var tasks = Enumerable.Range(0, parallelism).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(0, 5));
            await using var db = _fixture.NewContext();
            var tx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Provider = "stripe",
                ProviderTransactionId = "tx-" + i,
                Status = PaymentTransactionStatus.Succeeded,
                Amount = 50m,
                RawPayloadJson = "{}",
                IdempotencyKey = sharedKey,
            };
            db.Add(tx);
            try
            {
                await db.SaveChangesAsync();
                outcomes.Add("ok");
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                outcomes.Add("rejected");
            }
        }).ToArray();
        await Task.WhenAll(tasks);

        outcomes.Count(o => o == "ok").Should().Be(1,
            "only the first writer for a given (InvoiceId, IdempotencyKey) may persist; the rest must hit the unique-violation");
        outcomes.Count(o => o == "rejected").Should().Be(parallelism - 1);

        await using var verify = _fixture.NewContext();
        var rows = await verify.Set<PaymentTransaction>()
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.IdempotencyKey == sharedKey)
            .ToListAsync();
        rows.Should().HaveCount(1,
            "the database must store exactly one row per (InvoiceId, IdempotencyKey) pair");
    }

    [SkippableFact]
    public async Task RecordPayment_ParallelInsertDifferentIdempotencyKeys_AllPersist()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Companion test: when N webhooks present DIFFERENT idempotency
        // keys (a real "different transaction" scenario, not a replay),
        // every insert must persist. Catches a mutation that would
        // over-broaden the unique index to (InvoiceId) alone.
        var (courseId, semesterId, structureNodeId) = await SeedCourseSemesterNodeAsync();
        var studentId = await SeedStudentAsync(structureNodeId);
        var invoiceId = await SeedInvoiceAsync(studentId, totalAmount: 1000m);

        const int parallelism = 6;
        var tasks = Enumerable.Range(0, parallelism).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(0, 5));
            await using var db = _fixture.NewContext();
            var tx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Provider = "stripe",
                ProviderTransactionId = "tx-" + i,
                Status = PaymentTransactionStatus.Succeeded,
                Amount = 10m,
                RawPayloadJson = "{}",
                IdempotencyKey = "webhook-" + i, // DIFFERENT keys
            };
            db.Add(tx);
            await db.SaveChangesAsync();
        }).ToArray();
        await Task.WhenAll(tasks);

        await using var verify = _fixture.NewContext();
        var rows = await verify.Set<PaymentTransaction>()
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId)
            .ToListAsync();
        rows.Should().HaveCount(parallelism,
            "distinct idempotency keys must all persist — the unique index is on the COMPOSITE (InvoiceId, IdempotencyKey), not on InvoiceId alone");
    }

    // ---------------- SessionVersion — atomic ExecuteUpdateAsync ----------------

    [SkippableFact]
    public async Task IncrementVersion_50ParallelOnRealSql_FinalVersionEqualsBaselinePlusParallelism()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
        // Invariant: N concurrent writers each calling
        // IncrementVersionAsync against the same user-id must result in
        // the persisted SessionVersion ending at exactly baseline + N. The
        // production path uses ExecuteUpdateAsync (server-side
        // `SET SessionVersion = SessionVersion + 1`) under
        // IsRelational(), which holds a row-level lock for the duration
        // of each UPDATE. Without that atomicity (read-modify-write on
        // InMemory), 50 concurrent writers would race and the final
        // version would be ≤ baseline + N due to lost updates.
        var staffId = Guid.NewGuid();
        var structureNodeId = await SeedStructureNodeAsync();
        await SeedStaffAsync(staffId, structureNodeId, baselineVersion: 0);

        const int parallelism = 50;
        var tasks = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await Task.Delay(Random.Shared.Next(0, 5));
            await using var db = _fixture.NewContext();
            var svc = new SessionVersionService(db);
            await svc.IncrementVersionAsync(staffId);
        }).ToArray();
        await Task.WhenAll(tasks);

        await using var verify = _fixture.NewContext();
        var version = await verify.Staffs
            .AsNoTracking()
            .Where(s => s.Id == staffId)
            .Select(s => s.SessionVersion)
            .FirstAsync();
        version.Should().Be(parallelism,
            "ExecuteUpdateAsync produces a server-side `SessionVersion = SessionVersion + 1` UPDATE that holds a row lock — no two writers may overlap, so the final version is exactly baseline + N");
    }

    // ============================================================
    // Helpers
    // ============================================================

    // SQL Server error numbers for unique-key collision. The handler
    // unwraps the inner SqlException via Number to distinguish unique
    // violations from other DbUpdateExceptions.
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sql &&
               (sql.Number == SqlUniqueConstraintViolation ||
                sql.Number == SqlUniqueIndexViolation);
    }

    private async Task<(Guid CourseId, Guid SemesterId, Guid StructureNodeId)> SeedCourseSemesterNodeAsync()
    {
        await using var db = _fixture.NewContext();
        var node = NewStructureNode();
        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            Name = "{\"en\":\"2025\"}",
            StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
        };
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Name = "{\"en\":\"Fall\"}",
            StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            AcademicYearId = year.Id,
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "C-" + Guid.NewGuid().ToString("N").Substring(0, 6),
            Title = "{\"en\":\"Test\"}",
            CreditHours = 3,
        };
        db.AcademicYears.Add(year);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.StructureNodes.Add(node);
        await db.SaveChangesAsync();
        return (course.Id, semester.Id, node.Id);
    }

    private async Task<Guid> SeedStructureNodeAsync()
    {
        await using var db = _fixture.NewContext();
        var node = NewStructureNode();
        db.StructureNodes.Add(node);
        await db.SaveChangesAsync();
        return node.Id;
    }

    private static StructureNode NewStructureNode()
    {
        var id = Guid.NewGuid();
        return new StructureNode
        {
            Id = id,
            Name = "{\"en\":\"Test\"}",
            Type = StructureNodeType.Level,
            Path = "/" + id.ToString("N"),
            Depth = 1,
            Order = 0,
        };
    }

    private async Task<CourseOfferingEntity> SeedOfferingAsync(Guid courseId, Guid semesterId, Guid structureNodeId, int capacity)
    {
        await using var db = _fixture.NewContext();
        var offering = new CourseOfferingEntity
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            SemesterId = semesterId,
            StructureNodeId = structureNodeId,
            SectionCode = "A",
            Status = OfferingStatus.Open,
            RegistrationState = RegistrationState.Open,
        };
        offering.InitializeCapacity(capacity);
        db.Add(offering);
        await db.SaveChangesAsync();
        return offering;
    }

    private async Task<Guid> SeedStudentAsync(Guid structureNodeId)
    {
        await using var db = _fixture.NewContext();
        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "S-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            NationalId = Guid.NewGuid().ToString("N").Substring(0, 14),
            Name = "{\"en\":\"Test\"}",
            BirthDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNumber = "0100",
            Email = Guid.NewGuid().ToString("N") + "@t.eg",
            StructureNodeId = structureNodeId,
            PasswordHash = "x",
            IsActive = true,
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    private async Task<Guid> SeedInvoiceAsync(Guid studentId, decimal totalAmount)
    {
        await using var db = _fixture.NewContext();
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Status = InvoiceStatus.Pending,
            TotalAmount = totalAmount,
            Currency = "EGP",
        };
        db.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task SeedStaffAsync(Guid id, Guid structureNodeId, int baselineVersion)
    {
        await using var db = _fixture.NewContext();
        var staff = new Staff
        {
            Id = id,
            EmployeeCode = "E-" + Guid.NewGuid().ToString("N").Substring(0, 6),
            Name = "{\"en\":\"Tester\"}",
            NationalId = Guid.NewGuid().ToString("N").Substring(0, 14),
            BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNumber = "0100",
            Email = Guid.NewGuid().ToString("N") + "@t.eg",
            StructureNodeId = structureNodeId,
            PasswordHash = "x",
            IsActive = true,
            SessionVersion = baselineVersion,
        };
        db.Staffs.Add(staff);
        await db.SaveChangesAsync();
    }
}
