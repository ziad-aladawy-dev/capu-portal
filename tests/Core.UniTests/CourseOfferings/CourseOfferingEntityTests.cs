using CapitalUniversity.Modules.CourseOffering.Domain;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.CourseOfferings;

/// <summary>
/// CourseOffering entity invariants: capacity is non-negative, registration
/// count is bounded by capacity, capacity cannot shrink below the live count.
/// These are the only things the entity itself enforces — everything else is
/// policy and lives in the future Registration module.
/// </summary>
public class CourseOfferingEntityTests
{
    private static CourseOffering NewOffering(int capacity = 30)
    {
        var offering = new CourseOffering
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
        };
        offering.InitializeCapacity(capacity);
        return offering;
    }

    [Fact]
    public void InitializeCapacity_NegativeValue_Throws()
    {
        var offering = NewOffering();
        var act = () => offering.InitializeCapacity(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IncrementRegistration_RespectsCapacity()
    {
        var offering = NewOffering(capacity: 2);
        offering.IncrementRegistration();
        offering.IncrementRegistration();

        var act = offering.IncrementRegistration;
        act.Should().Throw<InvalidOperationException>("offering at capacity must refuse additional registrations");
        offering.RegisteredCount.Should().Be(2);
    }

    [Fact]
    public void DecrementRegistration_FloorsAtZero()
    {
        var offering = NewOffering();
        offering.DecrementRegistration();
        offering.RegisteredCount.Should().Be(0, "double-drop must not underflow the count");
    }

    [Fact]
    public void AdjustCapacity_BelowCurrentRegistrationCount_Throws()
    {
        var offering = NewOffering(capacity: 5);
        offering.IncrementRegistration();
        offering.IncrementRegistration();
        offering.IncrementRegistration();

        var act = () => offering.AdjustCapacity(2);
        act.Should().Throw<InvalidOperationException>(
            "shrinking below the live count would silently lose registered students");
        offering.Capacity.Should().Be(5);
    }

    [Fact]
    public void AdjustCapacity_EqualToRegistrationCount_IsAllowed()
    {
        var offering = NewOffering(capacity: 5);
        offering.IncrementRegistration();
        offering.IncrementRegistration();
        offering.AdjustCapacity(2);

        offering.Capacity.Should().Be(2);
    }

    // ----------------------------------------------------------------------
    // Boundary + observable-effect tests that exercise the exact branches
    // Stryker mutates. Each test kills a specific surviving mutant on the
    // entity — see comments in each test for which guard it pins.
    // ----------------------------------------------------------------------

    [Fact]
    public void InitializeCapacity_Zero_IsAllowed()
    {
        // Pins the boundary "< 0 → <= 0" mutation in InitializeCapacity.
        // A zero-capacity offering is a legitimate draft (e.g. waitlist-only
        // section); flipping the guard to "<= 0" would forbid that case.
        var offering = new CourseOffering
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
        };
        offering.InitializeCapacity(0);
        offering.Capacity.Should().Be(0);
        offering.RegisteredCount.Should().Be(0);
    }

    [Fact]
    public void AdjustCapacity_Zero_OnEmptyOffering_IsAllowed()
    {
        // Same boundary mutation, AdjustCapacity flavor. With no
        // registrations, dropping capacity to 0 is legal — flipping the
        // guard to "<= 0" would forbid it.
        var offering = NewOffering(capacity: 5);
        offering.AdjustCapacity(0);
        offering.Capacity.Should().Be(0);
    }

    [Fact]
    public void DecrementRegistration_PositiveCount_DecrementsByOne()
    {
        // The existing DecrementRegistration test starts at count = 0, which
        // hits the early-return and never executes the subtraction line. This
        // test exercises the actual "RegisteredCount -= 1" so a mutation that
        // flips it to "+= 1" can no longer survive.
        var offering = NewOffering(capacity: 5);
        offering.IncrementRegistration();
        offering.IncrementRegistration();
        offering.IncrementRegistration();

        offering.DecrementRegistration();

        offering.RegisteredCount.Should().Be(2);
    }
}

