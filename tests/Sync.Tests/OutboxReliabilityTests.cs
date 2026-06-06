using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Push;
using CapitalUniversity.Sync.Student.Sources;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Integration tests for the outbox <b>at-least-once + sink-idempotency</b>
/// reliability model. The classic transactional-outbox gap is the window
/// between (A) <c>sink.PushAsync</c> returning success and (B)
/// <c>db.SaveChangesAsync</c> persisting the resulting status flip — a crash
/// or DB error in that window leaves the outbox row Pending in the sync DB
/// while the external side effect has already happened. The next tick will
/// re-push the same row.
///
/// <para>
/// The platform's contract — pinned by these tests — is:
/// <list type="number">
///   <item>The outbox writer passes the outbox row's stable Guid <c>Id</c> as
///         the <c>idempotencyKey</c> argument to the sink.</item>
///   <item>The sink MUST treat a repeat call with the same key as a no-op
///         (matches HTTP <c>Idempotency-Key</c> semantics — what Stripe / AWS /
///         Twilio etc. honour).</item>
///   <item>Therefore: a SaveChanges failure after a successful push results in
///         exactly one external side effect, and the row eventually reaches
///         <c>Processed</c> on a subsequent tick where SaveChanges succeeds.</item>
/// </list>
/// </para>
///
/// <para>
/// The test uses an <see cref="ISaveChangesInterceptor"/> to inject a one-shot
/// SaveChanges failure into the first DbContext. A second DbContext sharing
/// the same in-memory store represents the next-tick replay — the row
/// re-loaded there is whatever was last <em>committed</em>, which is the
/// original Pending row (because the first context's mutation never made it
/// past the interceptor).
/// </para>
/// </summary>
public class OutboxReliabilityTests
{
    private readonly string _dbName = "OutboxReliability_" + Guid.NewGuid();
    private readonly InMemoryExternalStudentSink _sink = new();

    [Fact]
    public async Task SaveChangesFails_AfterSuccessfulPush_NextTickReplaysWithoutDuplicateSideEffect()
    {
        // ── Seed ────────────────────────────────────────────────────────────
        // A clean DbContext writes one Pending outbox row to the shared
        // in-memory store. Both later contexts attach to this same store via
        // the shared database name.
        await using (var seedDb = NewDbContext(failSaveOnce: false))
        {
            await SeedPendingRowAsync(seedDb, "EXT-S-RELIABILITY-0001");
        }

        // ── Tick 1: push succeeds, SaveChanges injected to fail ─────────────
        // Writer.UpsertBatchAsync awaits sink.PushAsync, mutates the row in
        // memory, then awaits SaveChanges — the interceptor throws and the
        // mutation never lands in the in-memory store.
        var tick1Logger = new Mock<ILogger<StudentOutboxWriter>>().Object;
        StudentOutboxEntity row;
        await using (var brokenDb = NewDbContext(failSaveOnce: true))
        {
            row = await brokenDb.StudentOutbox.AsTracking().SingleAsync();
            var writer = new StudentOutboxWriter(brokenDb, _sink, tick1Logger);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => writer.UpsertBatchAsync(new[] { BuildDispatch(row) }, CancellationToken.None));
            ex.Message.Should().Contain("Simulated DB failure after external push");
        }

        // Sink was invoked exactly once and recorded the side effect — this is
        // the "external system already saw the change" half of the gap.
        _sink.PushInvocationCount.Should().Be(1, "sink was called once on tick 1");
        _sink.AcceptedCount.Should().Be(1, "exactly one external side effect");
        _sink.Accepted.Should().ContainKey("EXT-S-RELIABILITY-0001");

        // ── Verify state after the broken tick ──────────────────────────────
        // A fresh read-only context against the same in-memory store reflects
        // what was COMMITTED — the original Pending row, untouched (mutation
        // was rolled back when SaveChanges threw).
        await using (var inspectDb = NewDbContext(failSaveOnce: false))
        {
            var afterTick1 = await inspectDb.StudentOutbox.AsNoTracking().SingleAsync();
            afterTick1.Status.Should().Be(OutboxStatus.Pending,
                "the writer's status flip never reached the DB — row is exactly as seeded");
            afterTick1.AttemptCount.Should().Be(0,
                "the failure caught by SaveChanges interceptor isn't a sink failure — AttemptCount was never bumped");
            afterTick1.ProcessedAt.Should().BeNull();
            afterTick1.LastError.Should().BeNull();
            afterTick1.Id.Should().Be(row.Id, "same physical row picked up by tick 2");
        }

        // ── Tick 2: replay with a healthy DB ────────────────────────────────
        // The pipeline's next recurring tick re-extracts Pending rows. The
        // writer pushes the same row again with the SAME idempotency key (the
        // row's stable Id) — sink short-circuits via the seen-keys cache. No
        // duplicate external side effect.
        await using (var workingDb = NewDbContext(failSaveOnce: false))
        {
            var rowForTick2 = await workingDb.StudentOutbox.AsTracking().SingleAsync();
            var writer2 = new StudentOutboxWriter(workingDb, _sink, tick1Logger);

            var processed = await writer2.UpsertBatchAsync(
                new[] { BuildDispatch(rowForTick2) }, CancellationToken.None);
            processed.Should().Be(1, "writer treats the dedup'd push as success — sink returned without throwing");
        }

        // ── Assert the core invariant ───────────────────────────────────────
        // Sink was called twice (once per tick) but only one external side
        // effect was recorded — the second call was short-circuited by the
        // idempotency key. THIS IS WHAT KEEPS THE EXTERNAL SYSTEM FROM SEEING
        // A DUPLICATE.
        _sink.PushInvocationCount.Should().Be(2, "writer called sink on both ticks");
        _sink.AcceptedCount.Should().Be(1, "idempotency key on the second call dedup'd it — exactly one external side effect total");

        // Outbox row in the DB is now Processed — tick 2's SaveChanges
        // succeeded, so the status flip survived.
        await using (var finalDb = NewDbContext(failSaveOnce: false))
        {
            var finalRow = await finalDb.StudentOutbox.AsNoTracking().SingleAsync();
            finalRow.Status.Should().Be(OutboxStatus.Processed);
            finalRow.ProcessedAt.Should().NotBeNull();
            finalRow.LastError.Should().BeNull();
            finalRow.AttemptCount.Should().Be(0,
                "no per-row failures occurred — neither tick bumped AttemptCount");
        }
    }

    // ── Test infrastructure ─────────────────────────────────────────────────

    /// <summary>
    /// EF Core <see cref="ISaveChangesInterceptor"/> that throws on the FIRST
    /// SavingChangesAsync call and lets subsequent calls through. Simulates a
    /// transient DB blip that crashes the writer's commit after a successful
    /// external push.
    /// </summary>
    private sealed class ThrowOnceOnSaveInterceptor : ISaveChangesInterceptor
    {
        private int _invocations;

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _invocations) == 1)
            {
                throw new InvalidOperationException(
                    "Simulated DB failure after external push — interceptor armed once.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private StudentSyncDbContext NewDbContext(bool failSaveOnce)
    {
        var builder = new DbContextOptionsBuilder<StudentSyncDbContext>()
            .UseInMemoryDatabase(_dbName);

        if (failSaveOnce)
        {
            builder.AddInterceptors(new ThrowOnceOnSaveInterceptor());
        }

        return new StudentSyncDbContext(builder.Options);
    }

    private static async Task<StudentOutboxEntity> SeedPendingRowAsync(
        StudentSyncDbContext db, string externalId)
    {
        var payload = new ExternalStudent
        {
            ExternalStudentId = externalId,
            StudentCode = $"STU-{externalId}",
            Name = "Reliability Student",
            NationalId = $"NID-{externalId}",
            BirthDate = new DateTime(2001, 1, 1),
            PhoneNumber = "+200000000000",
            Email = externalId.ToLowerInvariant() + "@university.test",
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
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.StudentOutbox.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static StudentOutboxDispatch BuildDispatch(StudentOutboxEntity row)
    {
        var payload = OutboxPayloadSerializer.Deserialize<ExternalStudent>(row.Payload);
        return new StudentOutboxDispatch { Row = row, Payload = payload };
    }
}
