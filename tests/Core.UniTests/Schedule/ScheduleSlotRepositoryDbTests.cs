using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.UniTests._TestInfra;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Modules.Schedule.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Schedule;

/// <summary>
/// Pins repository + EF-configuration behavior against a real
/// <see cref="CoreDbContext"/> backed by the in-memory provider. The unique
/// index on <c>(CourseOfferingId, DayOfWeek, StartTime, EndTime)</c> is the
/// only structural guard against accidental duplicate rows — a refactor that
/// drops it from <c>ScheduleSlotConfiguration</c> would slip past mock-based
/// service tests.
/// </summary>
public class ScheduleSlotRepositoryDbTests : IDisposable
{
    private readonly CoreDbContext _context;
    private readonly ScheduleSlotRepository _repo;

    public ScheduleSlotRepositoryDbTests()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"schedule-slots-{Guid.NewGuid():N}")
            .Options;

        // Serialise the static check-then-add — see ModuleAssemblyRegistration
        // docstring for the parallel-fixture race this prevents.
        ModuleAssemblyRegistration.Ensure(typeof(ScheduleSlot).Assembly);

        _context = new CoreDbContext(options);
        _repo = new ScheduleSlotRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ScheduleSlot NewSlot(
        Guid? offeringId = null,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        int startHour = 9,
        int endHour = 10,
        ScheduleSlotKind kind = ScheduleSlotKind.Lecture)
    {
        var slot = new ScheduleSlot
        {
            CourseOfferingId = offeringId ?? Guid.NewGuid(),
            DayOfWeek = dayOfWeek,
            Kind = kind,
        };
        slot.SetTimeRange(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));
        return slot;
    }

    [Fact]
    public async Task Add_RoundTrips_TimeOnlyValues()
    {
        // TimeOnly + DayOfWeek + the enum kind must all serialize through EF
        // and come back identical — a converter regression on any of these
        // would corrupt the timetable on the next read.
        var offeringId = Guid.NewGuid();
        var slot = NewSlot(offeringId, DayOfWeek.Wednesday, 14, 16, ScheduleSlotKind.Lab);
        await _repo.AddAsync(slot);
        await _context.SaveChangesAsync();

        var loaded = await _repo.GetByIdAsync(slot.Id);
        loaded.Should().NotBeNull();
        loaded!.CourseOfferingId.Should().Be(offeringId);
        loaded.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        loaded.StartTime.Should().Be(new TimeOnly(14, 0));
        loaded.EndTime.Should().Be(new TimeOnly(16, 0));
        loaded.Kind.Should().Be(ScheduleSlotKind.Lab);
    }

    [Fact]
    public async Task GetForOffering_SortsByDayThenStart()
    {
        // The weekly-timetable render relies on this order. A consumer that
        // displays slots in insertion order would surface as a UI bug; pin it
        // here so a refactor to GetForOfferingAsync cannot quietly change the
        // order.
        var offeringId = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Wednesday, 9, 10));
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 14, 16));
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var slots = await _repo.GetForOfferingAsync(offeringId);

        slots.Select(s => (s.DayOfWeek, s.StartTime))
             .Should().Equal(
                (DayOfWeek.Monday, new TimeOnly(9, 0)),
                (DayOfWeek.Monday, new TimeOnly(14, 0)),
                (DayOfWeek.Wednesday, new TimeOnly(9, 0)));
    }

    [Fact]
    public async Task GetForOffering_IsScopedToTheRequestedOffering()
    {
        // Two offerings, one slot each; querying one must not return the
        // other. Without this, a refactor that drops the WHERE clause from
        // GetForOfferingAsync would broadcast every slot to every caller.
        var offeringA = Guid.NewGuid();
        var offeringB = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringA, DayOfWeek.Monday));
        await _repo.AddAsync(NewSlot(offeringB, DayOfWeek.Monday));
        await _context.SaveChangesAsync();

        var slotsA = await _repo.GetForOfferingAsync(offeringA);
        var slotsB = await _repo.GetForOfferingAsync(offeringB);

        slotsA.Should().HaveCount(1);
        slotsB.Should().HaveCount(1);
        slotsA.Single().CourseOfferingId.Should().Be(offeringA);
        slotsB.Single().CourseOfferingId.Should().Be(offeringB);
    }

    [Fact]
    public async Task Exists_ReturnsTrue_WhenSameTupleAlreadyPresent()
    {
        var offeringId = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var hit = await _repo.ExistsAsync(offeringId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0));
        hit.Should().BeTrue();
    }

    [Fact]
    public async Task Exists_ReturnsFalse_OnSameOfferingDifferentTime()
    {
        // The duplicate guard fires only on the full (offering, day, start, end)
        // tuple — two distinct slots at different times must coexist. If the
        // index ever degenerates to "one slot per day", this fails.
        var offeringId = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var hit = await _repo.ExistsAsync(offeringId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0));
        hit.Should().BeFalse();
    }

    // ----- Conflict (half-open overlap) detection -----

    [Fact]
    public async Task HasConflict_AdjacentSlot_NotAConflict()
    {
        // Existing 09:00-10:00 and proposed 10:00-11:00 share a boundary but
        // do not overlap. The strict inequality on the WHERE clause is the
        // entire reason adjacency is allowed — if a refactor flips < to <=,
        // this test fails immediately.
        var offeringId = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var hit = await _repo.HasConflictAsync(offeringId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0));
        hit.Should().BeFalse("10:00 end == 10:00 start is adjacency, not overlap — strict-< must reject the conflict claim");
    }

    [Theory]
    [InlineData(9, 30, 10, 30, "tail of new overlaps head of existing")]
    [InlineData(8, 30, 9, 30, "head of new overlaps tail of existing")]
    [InlineData(9, 15, 9, 45, "new fully contained within existing")]
    [InlineData(8, 30, 10, 30, "new fully spans existing")]
    [InlineData(9, 0, 10, 0,   "exact same window")]
    public async Task HasConflict_OverlapVariants_AllConflict(int startH, int startM, int endH, int endM, string reason)
    {
        // Pins every meaningful overlap shape against the same existing slot.
        // The single existing slot (09:00-10:00) is the fixture. Each row
        // exercises one geometric case that the < / > predicate must catch.
        // If the predicate degenerates (drops a side, flips an operator),
        // exactly one of these rows will start failing.
        var offeringId = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var hit = await _repo.HasConflictAsync(
            offeringId, DayOfWeek.Monday,
            new TimeOnly(startH, startM), new TimeOnly(endH, endM));
        hit.Should().BeTrue(reason);
    }

    [Fact]
    public async Task HasConflict_DifferentDay_IsNotAConflict()
    {
        var offeringId = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringId, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var hit = await _repo.HasConflictAsync(offeringId, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0));
        hit.Should().BeFalse(
            "conflict scope is (offering, day) — the same wall-clock time on a different weekday is unrelated");
    }

    [Fact]
    public async Task HasConflict_DifferentOffering_IsNotAConflict()
    {
        // Two offerings can run 09:00-10:00 on Monday at the same time. This
        // is the rule that keeps the Schedule module from drifting into
        // room/instructor de-confliction — those are different modules.
        var offeringA = Guid.NewGuid();
        var offeringB = Guid.NewGuid();
        await _repo.AddAsync(NewSlot(offeringA, DayOfWeek.Monday, 9, 10));
        await _context.SaveChangesAsync();

        var hit = await _repo.HasConflictAsync(offeringB, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0));
        hit.Should().BeFalse();
    }

    [Fact]
    public async Task HasConflict_ExcludeId_ExcludesOnlyThatRow()
    {
        // Two overlapping rows already in the DB (impossible under the unique
        // index, but we seed both to test the exclude logic in isolation):
        // - row A: 09:00-10:00 (will be the "self" we exclude)
        // - row B: 09:30-10:30 (the genuine collision)
        // Querying with excludeId=A.Id must still see B.
        var offeringId = Guid.NewGuid();
        var rowA = NewSlot(offeringId, DayOfWeek.Monday, 9, 10);
        var rowB = NewSlot(offeringId, DayOfWeek.Monday, 0, 1);
        rowB.SetTimeRange(new TimeOnly(9, 30), new TimeOnly(10, 30));
        await _repo.AddAsync(rowA);
        await _repo.AddAsync(rowB);
        await _context.SaveChangesAsync();

        var hit = await _repo.HasConflictAsync(
            offeringId, DayOfWeek.Monday,
            new TimeOnly(9, 0), new TimeOnly(10, 0),
            excludeId: rowA.Id);
        hit.Should().BeTrue(
            "excludeId must only ignore the named row — a genuine third-party overlap must still register");

        var hitExcludingBoth = await _repo.HasConflictAsync(
            offeringId, DayOfWeek.Monday,
            new TimeOnly(9, 0), new TimeOnly(10, 0),
            excludeId: rowB.Id);
        // Excluding B leaves A as the sole candidate, which matches the
        // proposed window exactly — that IS an overlap (same-window case).
        hitExcludingBoth.Should().BeTrue();
    }

    [Fact]
    public async Task HasConflict_NoMatchingOffering_ReturnsFalse()
    {
        var hit = await _repo.HasConflictAsync(
            Guid.NewGuid(), DayOfWeek.Monday,
            new TimeOnly(9, 0), new TimeOnly(10, 0));
        hit.Should().BeFalse();
    }
}