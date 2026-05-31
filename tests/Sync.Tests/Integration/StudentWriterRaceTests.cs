using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Pull;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapitalUniversity.Sync.Tests.Integration;

/// <summary>
/// Exercises the unique-constraint race-recovery path that EF Core's InMemory
/// provider cannot model (HasIndex(...).IsUnique() is a no-op there). Runs
/// against a real SQL Server Testcontainer so the production catch-and-retry
/// branch is actually executed.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class StudentWriterRaceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fx;
    private DbContextOptions<StudentSyncDbContext>? _dbOptions;

    public StudentWriterRaceTests(SqlServerFixture fx)
    {
        _fx = fx;
    }

    public async Task InitializeAsync()
    {
        if (_fx.DockerUnavailable) return;

        _dbOptions = new DbContextOptionsBuilder<StudentSyncDbContext>()
            .UseSqlServer(_fx.ConnectionString)
            .Options;

        await using var db = new StudentSyncDbContext(_dbOptions);
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpsertBatchAsync_ConcurrentInsertSameExternalId_RecoversViaRetry()
    {
        if (_fx.DockerUnavailable)
        {
            // Cleanly skip without failing the suite.
            return;
        }

        // Two writers, two DbContexts, both presented with the same ExternalStudentId.
        // The first to SaveChanges wins; the second hits the unique-index violation,
        // ChangeTracker.Clears, re-reads, and converges via external-wins update.
        var externalId = $"RACE-{Guid.NewGuid():N}";

        await using var dbA = new StudentSyncDbContext(_dbOptions!);
        await using var dbB = new StudentSyncDbContext(_dbOptions!);

        var writerA = new StudentWriter(dbA, NullLogger<StudentWriter>.Instance);
        var writerB = new StudentWriter(dbB, NullLogger<StudentWriter>.Instance);

        var batchA = new[]
        {
            new StudentEntity { ExternalStudentId = externalId, FirstName = "Alice", LastName = "A", Email = "a@x.test" }
        };
        var batchB = new[]
        {
            new StudentEntity { ExternalStudentId = externalId, FirstName = "Bob", LastName = "B", Email = "b@x.test" }
        };

        // Race: launch both concurrently. Test passes as long as both calls return
        // successfully (the loser is the one that took the retry path).
        var results = await Task.WhenAll(
            writerA.UpsertBatchAsync(batchA, CancellationToken.None),
            writerB.UpsertBatchAsync(batchB, CancellationToken.None));

        results[0].Should().Be(1);
        results[1].Should().Be(1);

        await using var verify = new StudentSyncDbContext(_dbOptions!);
        var rows = await verify.Students
            .Where(s => s.ExternalStudentId == externalId)
            .ToListAsync();

        rows.Should().HaveCount(1, "the writer's retry path must converge on a single row");
    }
}