using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Push;
using CapitalUniversity.Sync.Student.Sources;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Verifies the outbox-row retry contract that powers the Push pipeline:
///   • A transient sink failure does NOT abort the batch — the row stays Pending,
///     <c>AttemptCount</c> increments, and the next push tick re-attempts.
///   • Once <c>AttemptCount</c> reaches <see cref="StudentOutboxEntity.MaxAttempts"/>
///     the row is poisoned to <see cref="OutboxStatus.Failed"/> and stops being
///     picked up — manual intervention takes over.
///   • The writer is partial-batch isolated: one failed row does not roll back
///     successful peers in the same batch.
/// </summary>
public class OutboxRetryTests
{
    private readonly StudentSyncDbContext _db;
    private readonly InMemoryExternalStudentSink _sink;
    private readonly StudentOutboxWriter _writer;

    public OutboxRetryTests()
    {
        var options = new DbContextOptionsBuilder<StudentSyncDbContext>()
            .UseInMemoryDatabase("Outbox_" + Guid.NewGuid())
            .Options;
        _db = new StudentSyncDbContext(options);
        _sink = new InMemoryExternalStudentSink();
        _writer = new StudentOutboxWriter(_db, _sink, new Mock<ILogger<StudentOutboxWriter>>().Object);
    }

    [Fact]
    public async Task SinkThrowsOnce_AttemptCountBumps_RowStaysPending()
    {
        // Arrange — one Pending outbox row whose sink push is armed to throw.
        var row = await SeedPendingRowAsync("EXT-S-0042");
        _sink.FailNextPushFor("EXT-S-0042");

        // Act — the pipeline would call UpsertBatchAsync once per tick.
        var processed = await _writer.UpsertBatchAsync(
            new[] { BuildDispatch(row) },
            CancellationToken.None);

        // Assert — writer absorbed the per-row exception and returned a lower count.
        processed.Should().Be(0, "the writer must not throw on a per-row sink failure");

        var refreshed = await _db.StudentOutbox.AsNoTracking().SingleAsync();
        refreshed.Status.Should().Be(OutboxStatus.Pending, "row must stay Pending so the next push tick retries it");
        refreshed.AttemptCount.Should().Be(1);
        refreshed.LastError.Should().Contain("armed failure");
        refreshed.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task RetryAcrossTwoTicks_SecondTickProcessesRow()
    {
        // Arrange — same row, fail-armed for the first tick only.
        var row = await SeedPendingRowAsync("EXT-S-0043");
        _sink.FailNextPushFor("EXT-S-0043");

        // Act tick 1 — sink throws; outbox preserves the row for retry.
        var tick1 = await _writer.UpsertBatchAsync(
            new[] { BuildDispatch(row) },
            CancellationToken.None);
        tick1.Should().Be(0);

        // The pipeline re-extracts on the next recurring tick. Simulate that by
        // re-reading the row through a fresh tracked instance — the second tick
        // would pull Pending rows from the extractor, not reuse the prior batch.
        var rowForTick2 = await _db.StudentOutbox.SingleAsync();

        // Act tick 2 — no failure armed → sink accepts the push.
        var tick2 = await _writer.UpsertBatchAsync(
            new[] { BuildDispatch(rowForTick2) },
            CancellationToken.None);

        // Assert — second tick processes the row; AttemptCount carries forward.
        tick2.Should().Be(1);
        var finalized = await _db.StudentOutbox.AsNoTracking().SingleAsync();
        finalized.Status.Should().Be(OutboxStatus.Processed);
        finalized.AttemptCount.Should().Be(1, "the successful attempt does not bump the counter; only failures do");
        finalized.LastError.Should().BeNull("a successful push clears the previous error");
        finalized.ProcessedAt.Should().NotBeNull();
        _sink.Accepted.Should().ContainKey("EXT-S-0043");
    }

    [Fact]
    public async Task AttemptCountReachesMax_RowIsPoisonedToFailed()
    {
        // Arrange — seed a row that has already burned MaxAttempts - 1 retries.
        // One more failure should poison it.
        var row = await SeedPendingRowAsync(
            "EXT-S-0044",
            attemptCount: StudentOutboxEntity.MaxAttempts - 1);

        _sink.FailNextPushFor("EXT-S-0044");

        // Act
        var processed = await _writer.UpsertBatchAsync(
            new[] { BuildDispatch(row) },
            CancellationToken.None);

        // Assert
        processed.Should().Be(0);
        var poisoned = await _db.StudentOutbox.AsNoTracking().SingleAsync();
        poisoned.Status.Should().Be(OutboxStatus.Failed, "row must transition to Failed once AttemptCount crosses MaxAttempts");
        poisoned.AttemptCount.Should().Be(StudentOutboxEntity.MaxAttempts);
        poisoned.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MixedBatch_OneFailure_DoesNotRollBackPeers()
    {
        // Arrange — three Pending rows; only the middle one's sink push fails.
        // Partial-batch isolation: the other two must still land as Processed.
        var rowA = await SeedPendingRowAsync("EXT-S-0050");
        var rowB = await SeedPendingRowAsync("EXT-S-0051");
        var rowC = await SeedPendingRowAsync("EXT-S-0052");
        _sink.FailNextPushFor("EXT-S-0051");

        // Act
        var processed = await _writer.UpsertBatchAsync(
            new[] { BuildDispatch(rowA), BuildDispatch(rowB), BuildDispatch(rowC) },
            CancellationToken.None);

        // Assert
        processed.Should().Be(2, "two rows succeeded; the third remains pending for a retry");

        var rows = await _db.StudentOutbox.AsNoTracking()
            .OrderBy(r => r.ExternalStudentId)
            .ToListAsync();
        rows.Should().HaveCount(3);

        rows[0].Status.Should().Be(OutboxStatus.Processed);
        rows[1].Status.Should().Be(OutboxStatus.Pending);
        rows[1].AttemptCount.Should().Be(1);
        rows[1].LastError.Should().NotBeNullOrEmpty();
        rows[2].Status.Should().Be(OutboxStatus.Processed);

        _sink.Accepted.Keys.Should().BeEquivalentTo(new[] { "EXT-S-0050", "EXT-S-0052" });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<StudentOutboxEntity> SeedPendingRowAsync(string externalId, int attemptCount = 0)
    {
        var payload = new ExternalStudent
        {
            ExternalStudentId = externalId,
            StudentCode = $"STU-{externalId}",
            Name = "Test Student",
            NationalId = $"NID-{externalId}",
            BirthDate = new DateTime(2001, 1, 1),
            PhoneNumber = "+200000000000",
            Email = externalId + "@university.test",
            IsActive = true,
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        };

        var row = new StudentOutboxEntity
        {
            Id = Guid.NewGuid(),
            ExternalStudentId = externalId,
            Operation = OutboxOperation.Upsert,
            Payload = OutboxPayloadSerializer.Serialize(payload),
            PayloadSchemaVersion = StudentOutboxEntity.CurrentPayloadSchemaVersion,
            Status = OutboxStatus.Pending,
            AttemptCount = attemptCount,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.StudentOutbox.Add(row);
        await _db.SaveChangesAsync();
        return row;
    }

    private static StudentOutboxDispatch BuildDispatch(StudentOutboxEntity row)
    {
        var payload = OutboxPayloadSerializer.Deserialize<ExternalStudent>(row.Payload);
        return new StudentOutboxDispatch { Row = row, Payload = payload };
    }
}
