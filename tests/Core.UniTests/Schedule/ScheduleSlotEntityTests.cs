using CapitalUniversity.Modules.Schedule.Domain;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Schedule;

/// <summary>
/// Invariants ScheduleSlot enforces on itself — the only logic the entity owns
/// is "end &gt; start". Everything else (conflict detection, room booking,
/// recurrence) is explicitly out of scope per the module's design contract.
/// </summary>
public class ScheduleSlotEntityTests
{
    private static ScheduleSlot NewSlot()
    {
        var slot = new ScheduleSlot
        {
            CourseOfferingId = Guid.NewGuid(),
            DayOfWeek = DayOfWeek.Monday,
        };
        slot.SetTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 30));
        return slot;
    }

    [Fact]
    public void SetTimeRange_HappyPath_StoresBothEndpoints()
    {
        var slot = NewSlot();
        slot.StartTime.Should().Be(new TimeOnly(9, 0));
        slot.EndTime.Should().Be(new TimeOnly(10, 30));
    }

    [Fact]
    public void SetTimeRange_EndBeforeStart_Throws()
    {
        var slot = NewSlot();
        var act = () => slot.SetTimeRange(new TimeOnly(11, 0), new TimeOnly(10, 0));
        act.Should().Throw<InvalidOperationException>(
            "a slot that ends before it starts is meaningless and must be rejected at the entity boundary");
    }

    [Fact]
    public void SetTimeRange_EndEqualsStart_Throws()
    {
        var slot = NewSlot();
        var act = () => slot.SetTimeRange(new TimeOnly(11, 0), new TimeOnly(11, 0));
        act.Should().Throw<InvalidOperationException>(
            "a zero-length slot conveys no information — equality must be rejected, not silently accepted");
    }

    [Fact]
    public void SetTimeRange_AtomicallySwapsBothEndpoints()
    {
        // If SetTimeRange were ever refactored into two field writes with a
        // throw in between, a failed call could leave the entity in a
        // half-mutated state. This asserts the all-or-nothing behavior by
        // forcing a rejection and then checking nothing moved.
        var slot = NewSlot();
        var originalStart = slot.StartTime;
        var originalEnd = slot.EndTime;

        var act = () => slot.SetTimeRange(new TimeOnly(15, 0), new TimeOnly(14, 0));
        act.Should().Throw<InvalidOperationException>();

        slot.StartTime.Should().Be(originalStart, "a rejected update must not partially mutate the entity");
        slot.EndTime.Should().Be(originalEnd, "a rejected update must not partially mutate the entity");
    }
}