using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.UniTests._TestInfra;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Core.UniTests.CourseOfferings;

/// <summary>
/// Pins the SQL-side behavior of the atomic <c>TryIncrementRegistrationAsync</c>
/// / <c>TryDecrementRegistrationAsync</c> primitives against a real EF
/// <see cref="CoreDbContext"/> backed by the in-memory provider.
///
/// <para>
/// These tests exist because the conditional <c>WHERE</c> clause inside
/// <c>ExecuteUpdateAsync</c> is exactly the kind of expression a careless
/// refactor can silently weaken (dropping the capacity guard, dropping the
/// cancelled guard, etc.). A mock-based test would not catch that — a real
/// DbContext does.
/// </para>
/// </summary>
public class CourseOfferingRepositoryDbTests : IDisposable
{
    private readonly CoreDbContext _context;
    private readonly CourseOfferingRepository _repo;

    public CourseOfferingRepositoryDbTests()
    {
        // Each fixture instance gets its own isolated in-memory database so
        // tests cannot leak rows into each other.
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"course-offerings-{Guid.NewGuid():N}")
            .Options;

        // Module assemblies must be registered before the first DbContext is
        // instantiated so EF picks up CourseOfferingConfiguration. The helper
        // serialises this check-then-add across parallel fixture ctors —
        // unlocked, two threads could either double-add the assembly or
        // mutate the list while EF is iterating it inside OnModelCreating.
        ModuleAssemblyRegistration.Ensure(typeof(CourseOfferingEntity).Assembly);

        _context = new CoreDbContext(options);
        _repo = new CourseOfferingRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<CourseOfferingEntity> SeedAsync(
        int capacity = 2,
        int initialRegistered = 0,
        OfferingStatus status = OfferingStatus.Open)
    {
        var offering = new CourseOfferingEntity
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
            Status = status,
        };
        offering.InitializeCapacity(capacity);
        for (var i = 0; i < initialRegistered; i++) offering.IncrementRegistration();
        await _context.Set<CourseOfferingEntity>().AddAsync(offering);
        await _context.SaveChangesAsync();
        return offering;
    }

    [Fact]
    public async Task TryIncrement_OnAvailableOffering_ReturnsTrueAndBumpsCount()
    {
        var offering = await SeedAsync(capacity: 2, initialRegistered: 0);

        var ok = await _repo.TryIncrementRegistrationAsync(offering.Id);

        ok.Should().BeTrue();
        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.RegisteredCount.Should().Be(1);
    }

    [Fact]
    public async Task TryIncrement_AtCapacity_ReturnsFalseAndDoesNotMutate()
    {
        var offering = await SeedAsync(capacity: 1, initialRegistered: 1);

        var ok = await _repo.TryIncrementRegistrationAsync(offering.Id);

        ok.Should().BeFalse("the WHERE clause must reject increment when RegisteredCount >= Capacity");
        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.RegisteredCount.Should().Be(1, "no over-increment is allowed at the capacity boundary");
    }

    [Fact]
    public async Task TryIncrement_OnCancelledOffering_ReturnsFalse()
    {
        var offering = await SeedAsync(capacity: 5, initialRegistered: 0);
        offering.Cancel();
        await _context.SaveChangesAsync();

        var ok = await _repo.TryIncrementRegistrationAsync(offering.Id);

        ok.Should().BeFalse("cancelled offerings must not accept further registrations");
        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.RegisteredCount.Should().Be(0);
    }

    [Fact]
    public async Task TryIncrement_OnUnknownOffering_ReturnsFalse()
    {
        var ok = await _repo.TryIncrementRegistrationAsync(Guid.NewGuid());
        ok.Should().BeFalse();
    }

    [Fact]
    public async Task TryDecrement_OnPositiveCount_ReturnsTrueAndDrops()
    {
        var offering = await SeedAsync(capacity: 5, initialRegistered: 2);

        var ok = await _repo.TryDecrementRegistrationAsync(offering.Id);

        ok.Should().BeTrue();
        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.RegisteredCount.Should().Be(1);
    }

    [Fact]
    public async Task TryDecrement_AtZero_ReturnsFalseAndDoesNotUnderflow()
    {
        var offering = await SeedAsync(capacity: 5, initialRegistered: 0);

        var ok = await _repo.TryDecrementRegistrationAsync(offering.Id);

        ok.Should().BeFalse();
        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.RegisteredCount.Should().Be(0, "double-drop must never make the count negative");
    }

    /// <summary>
    /// Capacity-boundary saturation test: against a real DbContext, fire
    /// <c>capacity + N</c> sequential <c>TryIncrement</c> calls and assert
    /// that exactly <c>capacity</c> succeed and the count never exceeds
    /// capacity. This is the test that catches a future refactor that
    /// accidentally drops the <c>RegisteredCount &lt; Capacity</c> guard from
    /// the WHERE clause — without this assertion, the SQL would happily
    /// over-increment.
    /// </summary>
    [Fact]
    public async Task TryIncrement_SaturationAtCapacity_NeverExceedsCapacity()
    {
        const int capacity = 5;
        var offering = await SeedAsync(capacity: capacity, initialRegistered: 0);

        var successes = 0;
        for (var i = 0; i < capacity + 3; i++)
        {
            if (await _repo.TryIncrementRegistrationAsync(offering.Id)) successes++;
        }

        successes.Should().Be(capacity);
        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.RegisteredCount.Should().Be(capacity);
    }

    // ----------------------------------------------------------------------
    // Non-Try repository methods — DB-backed so mutations to the WHERE clauses
    // and projections actually get exercised. Pre-this-change the methods were
    // only invoked through mocked services in unit tests, leaving every EF
    // expression tree untested by Stryker.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsMatchingRow()
    {
        var offering = await SeedAsync();
        var fetched = await _repo.GetByIdAsync(offering.Id);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(offering.Id);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        await SeedAsync();
        var fetched = await _repo.GetByIdAsync(Guid.NewGuid());
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_SoftDeletedRow_IsHiddenByGlobalFilter()
    {
        var offering = await SeedAsync();
        offering.IsDeleted = true;
        await _context.SaveChangesAsync();

        var fetched = await _repo.GetByIdAsync(offering.Id);
        fetched.Should().BeNull("the EF query filter must hide soft-deleted offerings from repository reads");
    }

    [Fact]
    public async Task GetForNodeSemesterAsync_ScopedByBothNodeAndSemester()
    {
        var node = Guid.NewGuid();
        var sem = Guid.NewGuid();
        var otherSem = Guid.NewGuid();
        var otherNode = Guid.NewGuid();

        // Three rows: one matches, one has the wrong semester, one has the
        // wrong node. Both axes of the WHERE must hold for inclusion.
        await SeedWithKeysAsync(node, sem, "A");
        await SeedWithKeysAsync(node, otherSem, "B");
        await SeedWithKeysAsync(otherNode, sem, "C");

        var list = await _repo.GetForNodeSemesterAsync(node, sem);

        list.Should().HaveCount(1);
        list[0].SectionCode.Should().Be("A");
    }

    [Fact]
    public async Task GetForNodeSemesterAsync_StatusFilter_RestrictsResults()
    {
        var node = Guid.NewGuid();
        var sem = Guid.NewGuid();
        await SeedWithKeysAsync(node, sem, "A", status: OfferingStatus.Draft);
        await SeedWithKeysAsync(node, sem, "B", status: OfferingStatus.Open);
        await SeedWithKeysAsync(node, sem, "C", status: OfferingStatus.Open);

        var open = await _repo.GetForNodeSemesterAsync(node, sem, OfferingStatus.Open);
        var draft = await _repo.GetForNodeSemesterAsync(node, sem, OfferingStatus.Draft);

        open.Should().HaveCount(2);
        open.Should().OnlyContain(o => o.Status == OfferingStatus.Open);
        draft.Should().HaveCount(1);
        draft[0].SectionCode.Should().Be("A");
    }

    [Fact]
    public async Task GetForNodeSemesterAsync_NullStatus_ReturnsAllStatuses()
    {
        var node = Guid.NewGuid();
        var sem = Guid.NewGuid();
        await SeedWithKeysAsync(node, sem, "A", status: OfferingStatus.Draft);
        await SeedWithKeysAsync(node, sem, "B", status: OfferingStatus.Open);

        var all = await _repo.GetForNodeSemesterAsync(node, sem, status: null);

        all.Should().HaveCount(2, "the null status filter must be a no-op, not a 'status = null' query");
    }

    [Fact]
    public async Task GetForCourseAsync_FiltersByCourseAndSemester_AcrossNodes()
    {
        var course = Guid.NewGuid();
        var sem = Guid.NewGuid();
        await SeedWithKeysAsync(Guid.NewGuid(), sem, "A", courseId: course);
        await SeedWithKeysAsync(Guid.NewGuid(), sem, "B", courseId: course);
        // Same course but different semester — must NOT match.
        await SeedWithKeysAsync(Guid.NewGuid(), Guid.NewGuid(), "C", courseId: course);
        // Same semester but different course — must NOT match.
        await SeedWithKeysAsync(Guid.NewGuid(), sem, "D", courseId: Guid.NewGuid());

        var list = await _repo.GetForCourseAsync(course, sem);

        list.Should().HaveCount(2);
        list.Select(o => o.SectionCode).Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public async Task SectionExistsAsync_TrueWhenAllFourKeysMatch()
    {
        var course = Guid.NewGuid();
        var sem = Guid.NewGuid();
        var node = Guid.NewGuid();
        await SeedWithKeysAsync(node, sem, "A", courseId: course);

        var exists = await _repo.SectionExistsAsync(course, sem, node, "A");
        exists.Should().BeTrue();
    }

    [Theory]
    [InlineData("B")]          // section differs
    [InlineData("a")]          // case differs — the unique index is case-sensitive at the column level
    public async Task SectionExistsAsync_FalseWhenAnyKeyDiffers(string queriedSection)
    {
        var course = Guid.NewGuid();
        var sem = Guid.NewGuid();
        var node = Guid.NewGuid();
        await SeedWithKeysAsync(node, sem, "A", courseId: course);

        var exists = await _repo.SectionExistsAsync(course, sem, node, queriedSection);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SectionExistsAsync_FalseWhenCourseDiffers()
    {
        var sem = Guid.NewGuid();
        var node = Guid.NewGuid();
        await SeedWithKeysAsync(node, sem, "A", courseId: Guid.NewGuid());

        var exists = await _repo.SectionExistsAsync(Guid.NewGuid(), sem, node, "A");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Update_PersistsMutation()
    {
        var offering = await SeedAsync(capacity: 5);
        offering.SectionCode = "B";
        _repo.Update(offering);
        await _context.SaveChangesAsync();

        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded!.SectionCode.Should().Be("B");
    }

    [Fact]
    public async Task Delete_RemovesRow()
    {
        var offering = await SeedAsync();
        _repo.Delete(offering);
        await _context.SaveChangesAsync();

        var reloaded = await _repo.GetByIdAsync(offering.Id);
        reloaded.Should().BeNull();
    }

    /// <summary>
    /// Like <see cref="SeedAsync"/> but pins the (course, semester, node,
    /// section) tuple so list / exists tests can build deterministic fixtures.
    /// </summary>
    private async Task<CourseOfferingEntity> SeedWithKeysAsync(
        Guid structureNodeId,
        Guid semesterId,
        string sectionCode,
        Guid? courseId = null,
        OfferingStatus status = OfferingStatus.Open,
        int capacity = 5)
    {
        var offering = new CourseOfferingEntity
        {
            CourseId = courseId ?? Guid.NewGuid(),
            SemesterId = semesterId,
            StructureNodeId = structureNodeId,
            SectionCode = sectionCode,
            Status = status,
        };
        offering.InitializeCapacity(capacity);
        await _context.Set<CourseOfferingEntity>().AddAsync(offering);
        await _context.SaveChangesAsync();
        return offering;
    }
}