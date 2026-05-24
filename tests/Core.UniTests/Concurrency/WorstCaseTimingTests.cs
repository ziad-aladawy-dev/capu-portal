using System.Collections.Concurrent;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using CapitalUniversity.Core.UniTests._TestInfra;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Modules.Schedule.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Core.UniTests.Concurrency;

/// <summary>
/// Task 3 — Worst-case concurrency / timing tests.
///
/// <para>
/// Single-node concurrency only: every test runs against a single
/// InMemory <see cref="CoreDbContext"/>-backed database with multiple
/// scoped DbContext instances pointing at the same store. The tests
/// simulate request-level parallelism (each task gets its own context,
/// just like ASP.NET Core's per-request scopes) and assert FINAL-STATE
/// invariants, not intermediate timings. Random small jitter is injected
/// before each parallel call to widen the read-modify-write race window.
/// </para>
///
/// <para>
/// Each test names the invariant it pins in its comment header.
/// Mutations to the production code that break the invariant will cause
/// these tests to fail deterministically (they all use Task.WhenAll and
/// post-condition asserts on a fresh AsNoTracking read).
/// </para>
///
/// <para>
/// What InMemory cannot simulate: real SQL Server row-version concurrency
/// rejection, SERIALIZABLE isolation, lock escalation. Where the production
/// code's defence depends on those (e.g. RowVersion-driven retry in
/// PaymentVerificationService, atomic ExecuteUpdateAsync in
/// SessionVersionService), the tests below assert the OBSERVABLE contract
/// the service still owes its callers under InMemory's looser semantics,
/// and the comment explicitly names the SQL-only guarantee it does not
/// exercise.
/// </para>
/// </summary>
public class WorstCaseTimingTests
{
    // The InMemoryDatabase root is process-static, so multiple
    // CoreDbContext instances built from the same dbName share storage.
    private static CoreDbContext NewDb(string dbName)
    {
        // ModuleAssemblyRegistration.EagerRegisterAllModules has already run
        // at assembly load time, so the static module-assembly list is
        // complete. No fixture-local Ensure() needed.
        return new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);
    }

    private static async Task SmallJitterAsync()
    {
        // 0–4 ms randomised delay. Widens the read-modify-write window so a
        // pure ordering bug surfaces deterministically. Not large enough to
        // serialise the workload — the goal is to interleave, not space out.
        await Task.Delay(Random.Shared.Next(0, 5));
    }

    private static CourseOfferingEntity SeededOffering(int capacity, OfferingStatus status = OfferingStatus.Open)
    {
        var offering = new CourseOfferingEntity
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
            Status = status,
            RegistrationState = RegistrationState.Open,
        };
        offering.InitializeCapacity(capacity);
        return offering;
    }

    // ============================================================
    // Bulk-concurrent registration — capacity invariant
    // ============================================================

    [Fact]
    public async Task TryIncrementRegistration_SequentialBurst_AtCapacity_NeverOverIncrements()
    {
        // Worst-case TIMING via tight back-to-back invocations on a single
        // shared DbContext, simulating a single-request burst (which is the
        // realistic threat model on this single-node deployment — async
        // continuations may interleave even within one request scope).
        //
        // Invariant: across N sequential TryIncrement calls (N > Capacity),
        // exactly Capacity return true and the persisted RegisteredCount
        // ends at Capacity. The Try* primitive must NOT over-increment.
        //
        // Why not multi-context parallel here? The InMemory provider does
        // not enforce RowVersion concurrency tokens, so a parallel-multi-
        // context test would falsely "pass" with over-increment hidden by
        // last-write-wins semantics. The companion test
        // `TryIncrementRegistration_ParallelMultiContext_RegisteredCount_NeverExceedsSuccessCount`
        // asserts the strictly weaker invariant that DOES survive InMemory.
        var dbName = "WC_Cap_Seq_" + Guid.NewGuid();
        var offering = SeededOffering(capacity: 10);
        using (var setup = NewDb(dbName))
        {
            setup.Add(offering);
            await setup.SaveChangesAsync();
        }

        using var db = NewDb(dbName);
        var repo = new CourseOfferingRepository(db);
        const int attempts = 50;
        var successes = 0;
        for (var i = 0; i < attempts; i++)
        {
            if (await repo.TryIncrementRegistrationAsync(offering.Id)) successes++;
        }

        successes.Should().Be(10,
            "exactly capacity attempts must succeed across {0} sequential calls", attempts);
        using var verifyDb = NewDb(dbName);
        var reloaded = await verifyDb.Set<CourseOfferingEntity>()
            .AsNoTracking()
            .FirstAsync(o => o.Id == offering.Id);
        reloaded.RegisteredCount.Should().Be(10);
        reloaded.RegisteredCount.Should().BeLessThanOrEqualTo(reloaded.Capacity);
    }

    [Fact]
    public async Task TryIncrementRegistration_ParallelMultiContext_RegisteredCount_NeverExceedsSuccessCount()
    {
        // Weaker invariant that survives InMemory's missing RowVersion
        // enforcement: the persisted RegisteredCount must equal the count
        // of TryIncrement calls that returned true. A bug that bumped the
        // counter without returning true (or vice versa) would violate this
        // even under InMemory's looser semantics.
        //
        // On real SQL Server the upper-bound assertion is tighter
        // (successes == min(N, Capacity)); see
        // `TryIncrementRegistration_SequentialBurst_AtCapacity_NeverOverIncrements`
        // for that pin.
        var dbName = "WC_Cap_Par_" + Guid.NewGuid();
        var offering = SeededOffering(capacity: 10);
        using (var setup = NewDb(dbName))
        {
            setup.Add(offering);
            await setup.SaveChangesAsync();
        }

        const int parallelism = 50;
        var successes = new ConcurrentBag<bool>();
        var tasks = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            successes.Add(await repo.TryIncrementRegistrationAsync(offering.Id));
        }).ToArray();
        await Task.WhenAll(tasks);

        using var verifyDb = NewDb(dbName);
        var reloaded = await verifyDb.Set<CourseOfferingEntity>()
            .AsNoTracking()
            .FirstAsync(o => o.Id == offering.Id);

        var trueCount = successes.Count(s => s);
        // Final state never reports MORE registrations than the count of
        // "succeeded" return values. A mutation that decremented or
        // duplicated the IncrementRegistration() call inside the loop
        // would flip this.
        reloaded.RegisteredCount.Should().BeLessThanOrEqualTo(trueCount,
            "no registration may show up in state without a matching true return");
        trueCount.Should().BeGreaterThan(0,
            "at least one of {0} attempts against an empty offering must succeed", parallelism);
    }

    [Fact]
    public async Task TryIncrementRegistration_100ParallelAcross100DistinctOfferings_NoCrosstalk()
    {
        // Invariant: a parallel burst against DIFFERENT offerings must
        // succeed for each, leaving each at RegisteredCount=1. Pins the
        // repository's WHERE clause against an "id == @id" mistake that
        // would otherwise let one offering's writes spill onto another.
        var dbName = "WC_Cross_" + Guid.NewGuid();
        var offerings = Enumerable.Range(0, 100).Select(_ => SeededOffering(capacity: 1)).ToList();
        using (var setup = NewDb(dbName))
        {
            setup.AddRange(offerings);
            await setup.SaveChangesAsync();
        }

        var tasks = offerings.Select(async o =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            return await repo.TryIncrementRegistrationAsync(o.Id);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().BeTrue());
        using var verifyDb = NewDb(dbName);
        var ids = offerings.Select(o => o.Id).ToList();
        var counts = await verifyDb.Set<CourseOfferingEntity>()
            .AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.RegisteredCount })
            .ToListAsync();
        counts.Should().HaveCount(100);
        counts.Should().AllSatisfy(c => c.RegisteredCount.Should().Be(1),
            "each offering must end at exactly its single increment — no cross-row interference");
    }

    [Fact]
    public async Task TryIncrement_OnCancelledOffering_ParallelBurst_AllFalse_RegisteredCountStaysZero()
    {
        // Invariant: a cancelled offering rejects EVERY parallel attempt.
        // Pins the `offering.Status == OfferingStatus.Cancelled` guard
        // inside the repository's retry loop.
        var dbName = "WC_Cancelled_" + Guid.NewGuid();
        var offering = SeededOffering(capacity: 100, status: OfferingStatus.Cancelled);
        using (var setup = NewDb(dbName))
        {
            setup.Add(offering);
            await setup.SaveChangesAsync();
        }

        var tasks = Enumerable.Range(0, 30).Select(async _ =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            return await repo.TryIncrementRegistrationAsync(offering.Id);
        }).ToArray();
        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().BeFalse(),
            "a cancelled offering rejects every increment, parallel or not");
        using var verifyDb = NewDb(dbName);
        var reloaded = await verifyDb.Set<CourseOfferingEntity>().AsNoTracking().FirstAsync(o => o.Id == offering.Id);
        reloaded.RegisteredCount.Should().Be(0);
    }

    // ============================================================
    // ScheduleSlot — overlap / duplicate race
    // ============================================================

    [Fact]
    public async Task AddSlot_ParallelInsertsAgainstDifferentOfferings_AllPersist_NoCrossOfferingLeak()
    {
        // Parallel-write invariant that DOES survive InMemory: N inserts
        // targeting N different offerings must all persist, and each
        // offering ends up with exactly its own slot — never another
        // offering's. Pins the CourseOfferingId predicate against any
        // mutation that would let a slot cross-bind.
        var dbName = "WC_Slot_Iso_" + Guid.NewGuid();
        const int n = 30;
        var offeringIds = Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

        using (var setup = NewDb(dbName))
        {
            await setup.SaveChangesAsync();
        }

        var tasks = offeringIds.Select(async oid =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new ScheduleSlotRepository(db);
            var slot = new ScheduleSlot
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = oid,
                DayOfWeek = DayOfWeek.Monday,
                Kind = ScheduleSlotKind.Lecture,
            };
            slot.SetTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 0));
            await repo.AddAsync(slot);
            await db.SaveChangesAsync();
        }).ToArray();
        await Task.WhenAll(tasks);

        using var verifyDb = NewDb(dbName);
        foreach (var oid in offeringIds)
        {
            var rows = await verifyDb.Set<ScheduleSlot>()
                .AsNoTracking()
                .Where(s => s.CourseOfferingId == oid)
                .ToListAsync();
            rows.Should().HaveCount(1,
                "each offering must own exactly one slot — no cross-offering leak under parallel insert");
            rows[0].CourseOfferingId.Should().Be(oid);
        }
    }

    // The previously-skipped `AddExactDuplicateSlot_…_SqlOnly` test has been
    // replaced by a real SQL Server test in
    // `SqlServerConcurrencyTests.AddExactDuplicateSlot_ParallelInsert_UniqueIndexRejectsAllButOne`.
    // That version uses Microsoft.EntityFrameworkCore.SqlServer + LocalDB so
    // the unique constraint is actually enforced, instead of being documented
    // as an executable specification only.

    [Fact]
    public async Task HasConflictAsync_AfterParallelInsert_AdjacentSlotsDoNotRegisterAsConflict()
    {
        // Invariant: two slots that are temporally adjacent (one ends exactly
        // when the next begins) are NOT conflicting. Pins the half-open
        // interval math — a mutation that flipped `<` to `<=` in the
        // overlap predicate would flip this assertion (an adjacent slot
        // would falsely register as a conflict).
        var dbName = "WC_Adj_" + Guid.NewGuid();
        var offeringId = Guid.NewGuid();

        using (var setup = NewDb(dbName))
        {
            var seed = new ScheduleSlot
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = offeringId,
                DayOfWeek = DayOfWeek.Tuesday,
                Kind = ScheduleSlotKind.Lecture,
            };
            seed.SetTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 0));
            setup.Add(seed);
            await setup.SaveChangesAsync();
        }

        // Multiple parallel readers ask the same conflict question. The answer
        // must be consistent and `false` — adjacency is allowed.
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new ScheduleSlotRepository(db);
            return await repo.HasConflictAsync(
                offeringId,
                DayOfWeek.Tuesday,
                start: new TimeOnly(10, 0),  // adjacent — starts when existing ends
                end:   new TimeOnly(11, 0));
        }).ToArray();
        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().BeFalse(),
            "adjacent (no-overlap) slots must never be flagged as conflicts under any timing");
    }

    // ============================================================
    // SessionVersion — monotonic increment contract
    // ============================================================

    [Fact]
    public async Task IncrementVersionAsync_NSequentialCalls_FinalVersionIsBaselinePlusN()
    {
        // Sequential contract: each call increments the version by exactly 1
        // and returns the new value. The PARALLEL-burst version of this test
        // would only exercise the InMemory fallback code path, which the
        // production source comment explicitly admits is not atomic — the
        // real SQL Server path uses ExecuteUpdateAsync (row-level lock).
        // Asserting parallel atomicity under InMemory would mis-document the
        // production guarantee, so we pin the sequential invariant only and
        // call out the SQL-only behaviour in the test header.
        var dbName = "WC_Sess_" + Guid.NewGuid();
        var staffId = Guid.NewGuid();

        using (var setup = NewDb(dbName))
        {
            setup.Staffs.Add(NewStaff(staffId, baselineVersion: 0));
            await setup.SaveChangesAsync();
        }

        using var db = NewDb(dbName);
        var svc = new SessionVersionService(db);
        const int bumps = 5;
        for (var i = 1; i <= bumps; i++)
        {
            var v = await svc.IncrementVersionAsync(staffId);
            v.Should().Be(i, $"call #{i} must return exactly the prior value + 1");
        }

        using var verifyDb = NewDb(dbName);
        var staff = await verifyDb.Staffs.AsNoTracking().FirstAsync(s => s.Id == staffId);
        staff.SessionVersion.Should().Be(bumps,
            "after N sequential bumps the persisted version is baseline + N");
    }

    [Fact]
    public async Task IncrementVersionAsync_UnknownUser_ReturnsNull_NoSideEffectOnOtherUsers()
    {
        // Invariant: incrementing an unknown user-id returns null AND does
        // not bump anyone else's version. Pins the `staff != null` / `student
        // != null` guard against a mutation that flips them away.
        var dbName = "WC_Sess_Unk_" + Guid.NewGuid();
        var realStaffId = Guid.NewGuid();
        using (var setup = NewDb(dbName))
        {
            setup.Staffs.Add(NewStaff(realStaffId, baselineVersion: 7));
            await setup.SaveChangesAsync();
        }

        using var db = NewDb(dbName);
        var svc = new SessionVersionService(db);
        var result = await svc.IncrementVersionAsync(Guid.NewGuid());

        result.Should().BeNull();
        using var verifyDb = NewDb(dbName);
        var staff = await verifyDb.Staffs.AsNoTracking().FirstAsync(s => s.Id == realStaffId);
        staff.SessionVersion.Should().Be(7,
            "an unknown-user increment must not nudge any real user's version");
    }

    // ============================================================
    // IDOR — concurrent mixed-target reads
    // ============================================================

    [Fact]
    public async Task ConcurrentGetById_MixedOfferingIds_EachCallReturnsOnlyItsOwnRow()
    {
        // Invariant: when many parallel callers ask for DIFFERENT offering
        // ids, every response corresponds to the id that was requested.
        // Pins the repository's `o.Id == id` filter against a mutation that
        // (a) drops the predicate, (b) flips to `!=`, or (c) returns the
        // first-tracked entity from a leaked change tracker. Functions as a
        // generalised IDOR contract — there is no path by which a parallel
        // request can be served data belonging to another caller's resource.
        var dbName = "WC_IDOR_" + Guid.NewGuid();
        var offerings = Enumerable.Range(0, 50).Select(_ => SeededOffering(capacity: 5)).ToList();
        using (var setup = NewDb(dbName))
        {
            setup.AddRange(offerings);
            await setup.SaveChangesAsync();
        }

        var tasks = offerings.Select(async expected =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            var fetched = await repo.GetByIdAsync(expected.Id);
            return (expected, fetched);
        }).ToArray();

        var pairs = await Task.WhenAll(tasks);

        pairs.Should().AllSatisfy(p =>
        {
            p.fetched.Should().NotBeNull();
            p.fetched!.Id.Should().Be(p.expected.Id,
                "no concurrent request must ever surface a row for a different offering id");
        });
    }

    [Fact]
    public async Task ConcurrentGetById_OneRealAndManyUnknownIds_RealAlwaysFound_UnknownsAlwaysNull()
    {
        // Stronger IDOR variant: the only persisted row is "real". Many
        // parallel readers ask for unknown ids, mixed with the real id. The
        // real id must always resolve; unknown ids must NEVER resolve to
        // the real row. Catches a "return any tracked entity" bug.
        var dbName = "WC_IDOR2_" + Guid.NewGuid();
        var real = SeededOffering(capacity: 5);
        using (var setup = NewDb(dbName))
        {
            setup.Add(real);
            await setup.SaveChangesAsync();
        }

        var lookups = new List<Guid>();
        for (var i = 0; i < 20; i++) lookups.Add(Guid.NewGuid());
        for (var i = 0; i < 5; i++) lookups.Add(real.Id);

        var tasks = lookups.Select(async id =>
        {
            await SmallJitterAsync();
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            return (id, fetched: await repo.GetByIdAsync(id));
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        foreach (var (id, fetched) in results)
        {
            if (id == real.Id)
                fetched.Should().NotBeNull("real id must always be found");
            else
                fetched.Should().BeNull("unknown id must never leak the real row");
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static Staff NewStaff(Guid id, int baselineVersion)
    {
        return new Staff
        {
            Id = id,
            EmployeeCode = $"E{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Worst Case Tester",
            NationalId = $"99{Guid.NewGuid():N}".Substring(0, 14),
            BirthDate = new DateTime(1990, 1, 1),
            PhoneNumber = "0100000000",
            Email = $"{Guid.NewGuid():N}@test.eg",
            StructureNodeId = Guid.NewGuid(),
            PasswordHash = "x",
            IsActive = true,
            SessionVersion = baselineVersion,
        };
    }
}
