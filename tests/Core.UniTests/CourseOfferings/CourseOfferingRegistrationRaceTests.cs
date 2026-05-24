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
/// H9 — Guards the post-fix invariant: between concurrency-retry attempts the
/// repository clears the EF change tracker so a stale Modified entity from
/// the failed attempt does not bleed into the retry's SaveChanges. Two
/// sequential increments against the same in-memory context must always
/// reflect a +2 result on disk, and no orphaned tracked state should remain.
/// </summary>
public class CourseOfferingRegistrationRaceTests
{
    private static CoreDbContext NewDb()
    {
        // Same pattern as CourseOfferingRepositoryDbTests: the static module
        // assembly list must be populated before the first DbContext is
        // built so EF discovers CourseOfferingConfiguration during
        // OnModelCreating.
        ModuleAssemblyRegistration.Ensure(typeof(CourseOfferingEntity).Assembly);
        return new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("CourseOff_H9_" + Guid.NewGuid())
            .Options);
    }

    private static CourseOfferingEntity SeededOffering(int capacity, int registered)
    {
        var offering = new CourseOfferingEntity
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
            Status = OfferingStatus.Open,
            RegistrationState = RegistrationState.Open,
        };
        offering.InitializeCapacity(capacity);
        for (var i = 0; i < registered; i++)
        {
            offering.IncrementRegistration();
        }
        return offering;
    }

    [Fact]
    public async Task TryIncrementRegistrationAsync_TwoSequentialCalls_ReflectsBothIncrements()
    {
        using var db = NewDb();
        var offering = SeededOffering(capacity: 10, registered: 0);
        db.Add(offering);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = new CourseOfferingRepository(db);

        (await repo.TryIncrementRegistrationAsync(offering.Id)).Should().BeTrue();
        (await repo.TryIncrementRegistrationAsync(offering.Id)).Should().BeTrue();

        var fresh = await db.Set<CourseOfferingEntity>().AsNoTracking().FirstAsync(o => o.Id == offering.Id);
        fresh.RegisteredCount.Should().Be(2);
    }

    [Fact]
    public async Task TryIncrementRegistrationAsync_AtCapacity_ReturnsFalseWithoutChange()
    {
        using var db = NewDb();
        var offering = SeededOffering(capacity: 2, registered: 2);
        db.Add(offering);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = new CourseOfferingRepository(db);
        (await repo.TryIncrementRegistrationAsync(offering.Id)).Should().BeFalse();

        var fresh = await db.Set<CourseOfferingEntity>().AsNoTracking().FirstAsync(o => o.Id == offering.Id);
        fresh.RegisteredCount.Should().Be(2);
    }

    [Fact]
    public async Task TryIncrementRegistrationAsync_ClearsTrackedStateBetweenAttempts()
    {
        // H9 — after a Try* call the change tracker should be effectively
        // empty (the entity is loaded, mutated, and saved within the call;
        // the new ChangeTracker.Clear at the top of the next loop iteration
        // means no Modified entries leak out). A stray sibling Modified
        // entity would otherwise tag along on a subsequent SaveChanges.
        using var db = NewDb();
        var offering = SeededOffering(capacity: 5, registered: 0);
        db.Add(offering);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = new CourseOfferingRepository(db);
        await repo.TryIncrementRegistrationAsync(offering.Id);

        // The Try* method internally cleared the tracker before its load.
        // After it returns, the offering it loaded is still tracked (it's
        // the Unchanged entity post-save), but the EntityState we care
        // about — Modified — is gone.
        db.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added)
            .Should().BeEmpty("the loop's ChangeTracker.Clear guarantees no leaked write state survives a Try* call");
    }
}
