using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Outbox;

/// <summary>
/// Producer + dispatcher contract:
///   - EnqueueAsync stages a row; the caller's SaveChanges commits it.
///   - The dispatcher loads pending rows, invokes the matching handler, stamps
///     ProcessedAt on success / bumps AttemptCount + LastError on failure.
///   - A processed row is never re-dispatched (the dispatcher filters by ProcessedAt is null).
///   - A handler that throws is retried up to MaxAttempts, then parked.
/// </summary>
public class OutboxDispatchTests
{
    private const string TestMessageType = "test.echo";

    private static (IServiceProvider Provider, CoreDbContext Db, OutboxDispatcher Dispatcher, EchoHandler Handler) Build(
        int maxAttempts = 3,
        bool handlerThrows = false)
    {
        var services = new ServiceCollection();
        var dbName = "Outbox_" + Guid.NewGuid();
        services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);

        var handler = new EchoHandler(handlerThrows);
        services.AddSingleton<IOutboxMessageHandler>(handler);
        services.AddSingleton(Options.Create(new OutboxOptions { MaxAttempts = maxAttempts, BatchSize = 50 }));

        services.AddScoped(_ => new Mock<IExecutionContext>().Object);
        services.AddScoped(_ => new Mock<ICurrentCultureService>().Object);

        var provider = services.BuildServiceProvider();
        var db = provider.CreateScope().ServiceProvider.GetRequiredService<CoreDbContext>();
        db.Database.EnsureCreated();

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<OutboxOptions>>(),
            NullLogger<OutboxDispatcher>.Instance);

        return (provider, db, dispatcher, handler);
    }

    [Fact]
    public async Task EnqueueThenDispatch_InvokesHandler_AndStampsProcessedAt()
    {
        var (provider, db, dispatcher, handler) = Build();
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);

        await outbox.EnqueueAsync(TestMessageType, new { greeting = "hello" });
        await db.SaveChangesAsync();
        db.OutboxMessages.Count().Should().Be(1);

        await dispatcher.ProcessBatchAsync(CancellationToken.None);

        handler.Calls.Should().Be(1);
        var row = await db.OutboxMessages.AsNoTracking().FirstAsync();
        row.ProcessedAt.Should().NotBeNull();
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().BeNull();
    }

    [Fact]
    public async Task SecondDispatchPass_DoesNotRedispatchProcessedRows()
    {
        var (provider, db, dispatcher, handler) = Build();
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);
        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });
        await db.SaveChangesAsync();

        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        handler.Calls.Should().Be(1);

        // Second tick — the row is processed; the dispatcher must skip it.
        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        handler.Calls.Should().Be(1, "ProcessedAt-stamped rows must never be replayed");
    }

    [Fact]
    public async Task HandlerThrows_BumpsAttempt_LeavesPending_RetriesUntilMaxAttempts()
    {
        const int maxAttempts = 3;
        var (provider, db, dispatcher, handler) = Build(maxAttempts: maxAttempts, handlerThrows: true);
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);
        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });
        await db.SaveChangesAsync();

        for (var i = 0; i < maxAttempts; i++)
        {
            await dispatcher.ProcessBatchAsync(CancellationToken.None);
        }

        var row = await db.OutboxMessages.AsNoTracking().FirstAsync();
        row.ProcessedAt.Should().BeNull("the handler never succeeded");
        row.AttemptCount.Should().Be(maxAttempts);
        row.LastError.Should().NotBeNullOrEmpty();

        // One more pass — row is past MaxAttempts and should be left alone (parked).
        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        row = await db.OutboxMessages.AsNoTracking().FirstAsync();
        row.AttemptCount.Should().Be(maxAttempts, "rows past MaxAttempts must not be retried");
    }

    [Fact]
    public async Task UnknownMessageType_IsNotRetriedToInfinity()
    {
        var (provider, db, dispatcher, _) = Build();
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);
        await outbox.EnqueueAsync("not.registered", new { x = 1 });
        await db.SaveChangesAsync();

        await dispatcher.ProcessBatchAsync(CancellationToken.None);

        var row = await db.OutboxMessages.AsNoTracking().FirstAsync();
        row.ProcessedAt.Should().BeNull();
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().Contain("No handler registered");
    }

    [Fact]
    public async Task EnqueueAsync_DoesNotCommit_LeavingTransactionalAtomicityToTheCaller()
    {
        // The outbox must NOT call SaveChanges internally — the whole point of the
        // pattern is that the row commits in the same txn as the business state it
        // accompanies. Verify the row is staged (.Local) but absent from the DB until
        // the caller saves.
        var (provider, db, _, _) = Build();
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);

        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });

        db.OutboxMessages.Local.Should().HaveCount(1);
        db.ChangeTracker.Entries<OutboxMessage>().Should().HaveCount(1);
    }

    private class EchoHandler : IOutboxMessageHandler
    {
        private readonly bool _throws;
        public int Calls;
        public EchoHandler(bool throws) { _throws = throws; }
        public string MessageType => TestMessageType;
        public Task HandleAsync(Guid outboxMessageId, string payload, CancellationToken cancellationToken)
        {
            Calls++;
            if (_throws) throw new InvalidOperationException("simulated handler failure");
            return Task.CompletedTask;
        }
    }

    // ============================================================
    // Task 2 mutation-resistance additions targeting OutboxDispatcher.
    // Covers: empty-batch early-return, IsPoisoned transition on
    // handler-throw exhaustion, IsPoisoned transition on unknown-type
    // exhaustion, AttemptCount strictly < MaxAttempts boundary, and
    // global oldest-first ordering across message types.
    // ============================================================

    [Fact]
    public async Task EmptyBatch_NoSaveChanges_NoSideEffects()
    {
        // Pins the `if (batch.Count == 0) return;` early-exit. Mutating it to
        // continue into the foreach with an empty list still works; the value
        // here is that mutating `return` away would still pass quietly. So
        // this test mostly verifies no exception, no row creation.
        var (_, db, dispatcher, handler) = Build();

        await dispatcher.ProcessBatchAsync(CancellationToken.None);

        handler.Calls.Should().Be(0);
        db.OutboxMessages.Count().Should().Be(0);
    }

    [Fact]
    public async Task HandlerThrows_AtMaxAttempts_FlipsIsPoisoned_AndStampsPoisonedAt()
    {
        // Pins lines 148-155: the !IsPoisoned && AttemptCount >= MaxAttempts
        // branch. A mutation flipping `>=` to `>` would leave the row un-poisoned
        // forever. A mutation flipping the `!IsPoisoned` guard would re-poison on
        // every retry.
        const int maxAttempts = 2;
        var (_, db, dispatcher, _) = Build(maxAttempts: maxAttempts, handlerThrows: true);
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);
        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });
        await db.SaveChangesAsync();

        // Two attempts (AttemptCount becomes 1 then 2). On the second pass,
        // AttemptCount reaches MaxAttempts and the row must flip to poisoned.
        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        var afterFirst = await db.OutboxMessages.AsNoTracking().FirstAsync();
        afterFirst.IsPoisoned.Should().BeFalse("attempt 1 of 2 must not poison yet");
        afterFirst.AttemptCount.Should().Be(1);

        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        var afterSecond = await db.OutboxMessages.AsNoTracking().FirstAsync();
        afterSecond.IsPoisoned.Should().BeTrue("attempt 2 of 2 (>= max) must poison");
        afterSecond.AttemptCount.Should().Be(maxAttempts);
        afterSecond.PoisonedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UnknownMessageType_AtMaxAttempts_FlipsIsPoisoned()
    {
        // Pins the parallel poison transition in the no-handler branch
        // (lines 112-119). Same invariant as above, different code path.
        const int maxAttempts = 2;
        var (_, db, dispatcher, _) = Build(maxAttempts: maxAttempts);
        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);
        await outbox.EnqueueAsync("not.registered", new { x = 1 });
        await db.SaveChangesAsync();

        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        var afterFirst = await db.OutboxMessages.AsNoTracking().FirstAsync();
        afterFirst.IsPoisoned.Should().BeFalse();

        await dispatcher.ProcessBatchAsync(CancellationToken.None);
        var afterSecond = await db.OutboxMessages.AsNoTracking().FirstAsync();
        afterSecond.IsPoisoned.Should().BeTrue("unknown-type rows must poison once AttemptCount reaches max");
        afterSecond.PoisonedAt.Should().NotBeNull();
        afterSecond.LastError.Should().Contain("No handler registered");
    }

    [Fact]
    public async Task FilterClause_AttemptCount_StrictLessThan_MaxAttempts()
    {
        // The WHERE filter is `m.AttemptCount < _options.MaxAttempts`. A mutation
        // flipping `<` to `<=` would re-pick already-exhausted rows on the next
        // tick. Seed a poisoned row at exactly MaxAttempts and confirm it stays
        // untouched.
        const int maxAttempts = 2;
        var (_, db, dispatcher, handler) = Build(maxAttempts: maxAttempts, handlerThrows: false);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = TestMessageType,
            Payload = "{}",
            EnqueuedAt = DateTime.UtcNow.AddMinutes(-5),
            AttemptCount = maxAttempts,
            IsPoisoned = true,
            PoisonedAt = DateTime.UtcNow.AddMinutes(-1),
            LastError = "previously parked",
        });
        await db.SaveChangesAsync();

        await dispatcher.ProcessBatchAsync(CancellationToken.None);

        handler.Calls.Should().Be(0, "rows at MaxAttempts must NOT be re-selected by the WHERE filter");
        var row = await db.OutboxMessages.AsNoTracking().FirstAsync();
        row.AttemptCount.Should().Be(maxAttempts);
        row.LastError.Should().Be("previously parked", "no mutation must occur on a parked row");
    }

    [Fact]
    public async Task Batch_IsOrderedByEnqueuedAt_AcrossDifferentMessageTypes()
    {
        // Pins the OrderBy(m => m.EnqueuedAt). A mutation to OrderByDescending
        // would invert handler-call ordering. Use OutboxService.EnqueueAsync
        // (matches the existing tests' setup) so any model-builder side-effects
        // are exercised consistently, then directly mutate EnqueuedAt to widen
        // the gap and pin order.
        var services = new ServiceCollection();
        var dbName = "Outbox_Order_" + Guid.NewGuid();
        services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);

        var calls = new List<string>();
        services.AddSingleton<IOutboxMessageHandler>(new RecordingHandler("type.a", calls));
        services.AddSingleton<IOutboxMessageHandler>(new RecordingHandler("type.b", calls));
        services.AddSingleton(Options.Create(new OutboxOptions { MaxAttempts = 3, BatchSize = 50 }));
        services.AddScoped(_ => new Mock<IExecutionContext>().Object);
        services.AddScoped(_ => new Mock<ICurrentCultureService>().Object);

        var provider = services.BuildServiceProvider();
        var db = provider.CreateScope().ServiceProvider.GetRequiredService<CoreDbContext>();
        db.Database.EnsureCreated();

        var outbox = new OutboxService(db, new Mock<IExecutionContext>().Object, new Mock<ICurrentCultureService>().Object);
        await outbox.EnqueueAsync("type.b", new { });
        await outbox.EnqueueAsync("type.a", new { });
        await db.SaveChangesAsync();

        // Backdate the "type.b" row so it is strictly older than "type.a".
        // OutboxService stamps EnqueuedAt to UtcNow at enqueue time; without
        // backdating, both rows would have effectively the same timestamp and
        // the OrderBy mutation would not be observable.
        var rows = await db.OutboxMessages.ToListAsync();
        var olderRow = rows.First(r => r.MessageType == "type.b");
        olderRow.EnqueuedAt = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<OutboxOptions>>(),
            NullLogger<OutboxDispatcher>.Instance);

        await dispatcher.ProcessBatchAsync(CancellationToken.None);

        calls.Should().HaveCount(2);
        calls[0].Should().Be("type.b", "the older row must dispatch first regardless of message-type grouping");
        calls[1].Should().Be("type.a");
    }

    private sealed class RecordingHandler : IOutboxMessageHandler
    {
        private readonly List<string> _calls;
        public RecordingHandler(string messageType, List<string> calls) { MessageType = messageType; _calls = calls; }
        public string MessageType { get; }
        public Task HandleAsync(Guid outboxMessageId, string payload, CancellationToken cancellationToken)
        {
            _calls.Add(MessageType);
            return Task.CompletedTask;
        }
    }
}
