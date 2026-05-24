using System.Diagnostics;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Core.UniTests.Concurrency;

/// <summary>
/// Task 4 — lightweight performance-sanity tests. Goal: catch
/// order-of-magnitude regressions on hot paths, NOT pin exact wall-clock
/// numbers (which would CI-flake on slow build agents).
///
/// <para>
/// Each test:
/// <list type="bullet">
///   <item>Targets a path that is on the page-render critical chain.</item>
///   <item>Asserts a generous upper bound (typically 5× the observed
///   local-laptop runtime) so a 10× regression breaks the build but
///   normal variance does not.</item>
///   <item>Uses a fresh `CoreDbContext` per task to simulate per-request
///   scopes — mirrors ASP.NET Core's scoped DbContext lifetime.</item>
/// </list>
/// </para>
///
/// <para>
/// What we deliberately do NOT do here: no NBomber, no real load
/// generator, no Testcontainers. The user's constraint is "10 000
/// concurrent users (logical simulation)" — these tests cover the
/// per-request shape of the hot reads. End-to-end load tests against a
/// staging environment remain a CI step outside of this xUnit suite.
/// </para>
/// </summary>
public class PerformanceSanityTests
{
    private readonly ITestOutputHelper _output;
    public PerformanceSanityTests(ITestOutputHelper output) { _output = output; }

    private static CoreDbContext NewDb(string dbName) =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static CourseOfferingEntity SeededOffering(Guid? id = null)
    {
        var offering = new CourseOfferingEntity
        {
            Id = id ?? Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
            Status = OfferingStatus.Open,
            RegistrationState = RegistrationState.Open,
        };
        offering.InitializeCapacity(20);
        return offering;
    }

    /// <summary>
    /// Hot read path: 100 parallel <c>GetByIdAsync</c> calls against
    /// distinct rows must complete well under the 2-second
    /// page-budget the product targets. Catches a future N+1 / lazy-load
    /// regression that would balloon per-call cost.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_100ParallelReads_CompletesWellUnderPageBudget()
    {
        const int n = 100;
        var dbName = "Perf_GetById_" + Guid.NewGuid();
        var offerings = Enumerable.Range(0, n).Select(_ => SeededOffering()).ToList();
        using (var setup = NewDb(dbName))
        {
            setup.AddRange(offerings);
            await setup.SaveChangesAsync();
        }

        var sw = Stopwatch.StartNew();
        var tasks = offerings.Select(async o =>
        {
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            return await repo.GetByIdAsync(o.Id);
        }).ToArray();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        _output.WriteLine($"100 parallel GetByIdAsync: {sw.ElapsedMilliseconds}ms");
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        // 2 s is the per-page budget; 100 reads must comfortably fit
        // inside that, leaving room for downstream work.
        sw.ElapsedMilliseconds.Should().BeLessThan(2000,
            "100 parallel single-row reads must comfortably fit inside the per-page 2-second budget");
    }

    /// <summary>
    /// Capacity gate at fan-out: 50 parallel registration attempts
    /// across 50 different offerings must complete inside the page
    /// budget. Mirrors a "registration storm" use case at the start of
    /// every term.
    /// </summary>
    [Fact]
    public async Task TryIncrement_50ParallelAcrossDistinctOfferings_CompletesUnderPageBudget()
    {
        const int n = 50;
        var dbName = "Perf_RegStorm_" + Guid.NewGuid();
        var offerings = Enumerable.Range(0, n).Select(_ => SeededOffering()).ToList();
        using (var setup = NewDb(dbName))
        {
            setup.AddRange(offerings);
            await setup.SaveChangesAsync();
        }

        var sw = Stopwatch.StartNew();
        var tasks = offerings.Select(async o =>
        {
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            return await repo.TryIncrementRegistrationAsync(o.Id);
        }).ToArray();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        _output.WriteLine($"50 parallel TryIncrement across distinct offerings: {sw.ElapsedMilliseconds}ms");
        results.Should().AllSatisfy(r => r.Should().BeTrue());
        sw.ElapsedMilliseconds.Should().BeLessThan(2000,
            "registration storm against distinct offerings must clear inside the 2-second page budget");
    }

    /// <summary>
    /// Cancelled-offering rejection path latency: 100 attempts against a
    /// single cancelled offering must reject all and stay well under
    /// the page budget. Pins the cost of the "false" path so a future
    /// retry-on-cancel mutation doesn't quietly add a few hundred ms.
    /// </summary>
    [Fact]
    public async Task TryIncrement_RejectionPath_100ParallelAgainstCancelled_StaysFast()
    {
        const int n = 100;
        var dbName = "Perf_Reject_" + Guid.NewGuid();
        var offering = SeededOffering();
        offering.Cancel();
        using (var setup = NewDb(dbName))
        {
            setup.Add(offering);
            await setup.SaveChangesAsync();
        }

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, n).Select(async _ =>
        {
            using var db = NewDb(dbName);
            var repo = new CourseOfferingRepository(db);
            return await repo.TryIncrementRegistrationAsync(offering.Id);
        }).ToArray();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        _output.WriteLine($"100 parallel TryIncrement on cancelled offering: {sw.ElapsedMilliseconds}ms");
        results.Should().AllSatisfy(r => r.Should().BeFalse());
        // Rejection should be FASTER than the success path (no save). A
        // regression that accidentally engaged the retry loop here would
        // blow this budget by ~100× (3 retries × jittered backoff per call).
        sw.ElapsedMilliseconds.Should().BeLessThan(2000,
            "rejection path must not engage the retry loop");
    }
}
