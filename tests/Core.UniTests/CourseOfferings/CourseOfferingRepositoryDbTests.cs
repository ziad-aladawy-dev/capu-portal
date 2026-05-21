using CapitalUniversity.Core.Infrastructure.Persistence;
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
        // instantiated so EF picks up CourseOfferingConfiguration. Registration
        // is idempotent (the module DI extension uses the same guard).
        var moduleAssembly = typeof(CourseOfferingEntity).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

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
}
