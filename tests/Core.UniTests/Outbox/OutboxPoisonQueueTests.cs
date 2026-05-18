using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Outbox;

/// <summary>
/// Poison-queue contract: a row that hits the retry cap is marked poisoned
/// exactly once (not on every subsequent tick), surfaces via
/// <see cref="IOutboxPoisonQueue.GetPoisonedAsync"/>, and can be requeued so
/// the dispatcher reattempts after the underlying defect is fixed.
/// </summary>
public class OutboxPoisonQueueTests
{
    private const string TestMessageType = "test.poison";

    private sealed class Harness : IDisposable
    {
        public IServiceProvider Provider { get; }
        public IServiceScope Scope { get; }
        public CoreDbContext Db { get; }
        public OutboxDispatcher Dispatcher { get; }

        public Harness(IServiceProvider provider, IServiceScope scope, CoreDbContext db, OutboxDispatcher dispatcher)
        {
            Provider = provider;
            Scope = scope;
            Db = db;
            Dispatcher = dispatcher;
        }

        public void Dispose()
        {
            Scope.Dispose();
            (Provider as IDisposable)?.Dispose();
        }
    }

    private static Harness Build(int maxAttempts, bool handlerThrows)
    {
        var services = new ServiceCollection();
        var dbName = "Poison_" + Guid.NewGuid();
        services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);
        services.AddSingleton<IOutboxMessageHandler>(new ThrowingHandler(TestMessageType, handlerThrows));
        services.AddSingleton(Options.Create(new OutboxOptions { MaxAttempts = maxAttempts, BatchSize = 50 }));

        var provider = services.BuildServiceProvider();
        // Hold the scope (and so the scoped DbContext) alive for the whole test —
        // letting it become GC-eligible mid-test silently no-ops SaveChanges
        // against the InMemory provider while reads keep appearing to work.
        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        db.Database.EnsureCreated();

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<OutboxOptions>>(),
            NullLogger<OutboxDispatcher>.Instance);

        return new Harness(provider, scope, db, dispatcher);
    }

    [Fact]
    public async Task HandlerThrows_AtMaxAttempts_RowIsMarkedPoisoned()
    {
        const int maxAttempts = 3;
        using var h = Build(maxAttempts, handlerThrows: true);
        var outbox = new OutboxService(h.Db);

        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });
        await h.Db.SaveChangesAsync();

        for (var i = 0; i < maxAttempts; i++) await h.Dispatcher.ProcessBatchAsync(CancellationToken.None);

        var row = await h.Db.OutboxMessages.AsNoTracking().FirstAsync();
        row.IsPoisoned.Should().BeTrue();
        row.PoisonedAt.Should().NotBeNull();
        row.AttemptCount.Should().Be(maxAttempts);
    }

    [Fact]
    public async Task PoisonedAt_IsStampedOnce_NotOverwrittenBySubsequentTicks()
    {
        const int maxAttempts = 2;
        using var h = Build(maxAttempts, handlerThrows: true);
        var outbox = new OutboxService(h.Db);
        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });
        await h.Db.SaveChangesAsync();

        for (var i = 0; i < maxAttempts; i++) await h.Dispatcher.ProcessBatchAsync(CancellationToken.None);
        var firstStamp = (await h.Db.OutboxMessages.AsNoTracking().FirstAsync()).PoisonedAt;

        // Subsequent ticks: row is past MaxAttempts so the dispatcher filters it out.
        await Task.Delay(15);
        await h.Dispatcher.ProcessBatchAsync(CancellationToken.None);

        var row = await h.Db.OutboxMessages.AsNoTracking().FirstAsync();
        row.PoisonedAt.Should().Be(firstStamp, "PoisonedAt is the first-failure stamp, not the last");
    }

    [Fact]
    public async Task GetPoisonedAsync_ReturnsParkedRowsOldestFirst()
    {
        const int maxAttempts = 1;
        using var h = Build(maxAttempts, handlerThrows: true);
        var outbox = new OutboxService(h.Db);

        await outbox.EnqueueAsync(TestMessageType, new { id = 1 });
        await h.Db.SaveChangesAsync();
        await h.Dispatcher.ProcessBatchAsync(CancellationToken.None);
        await Task.Delay(5);

        await outbox.EnqueueAsync(TestMessageType, new { id = 2 });
        await h.Db.SaveChangesAsync();
        await h.Dispatcher.ProcessBatchAsync(CancellationToken.None);

        var pq = new OutboxPoisonQueue(h.Db);
        var rows = await pq.GetPoisonedAsync();

        rows.Should().HaveCount(2);
        rows[0].PoisonedAt.Should().BeOnOrBefore(rows[1].PoisonedAt);
        rows.Should().AllSatisfy(r =>
        {
            r.MessageType.Should().Be(TestMessageType);
            r.LastError.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task RequeueAsync_ResetsAttemptCount_ClearsPoison_AllowsRetry()
    {
        const int maxAttempts = 1;
        using var h = Build(maxAttempts, handlerThrows: true);
        var outbox = new OutboxService(h.Db);
        await outbox.EnqueueAsync(TestMessageType, new { x = 1 });
        await h.Db.SaveChangesAsync();
        await h.Dispatcher.ProcessBatchAsync(CancellationToken.None);

        var row = await h.Db.OutboxMessages.AsNoTracking().FirstAsync();
        row.IsPoisoned.Should().BeTrue();

        var pq = new OutboxPoisonQueue(h.Db);
        var requeued = await pq.RequeueAsync(row.Id);
        requeued.Should().BeTrue();

        // Read via a fresh scope so we hit the shared InMemory store, not the
        // test DbContext's change tracker view of the just-updated entity.
        using var verifyScope = h.Provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var refreshed = await verifyDb.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == row.Id);
        refreshed.IsPoisoned.Should().BeFalse();
        refreshed.PoisonedAt.Should().BeNull();
        refreshed.AttemptCount.Should().Be(0);
        refreshed.LastError.Should().BeNull();
    }

    [Fact]
    public async Task RequeueAsync_ReturnsFalse_ForUnknownOrProcessedRows()
    {
        using var h = Build(maxAttempts: 1, handlerThrows: false);
        var pq = new OutboxPoisonQueue(h.Db);

        var missing = await pq.RequeueAsync(Guid.NewGuid());
        missing.Should().BeFalse();

        h.Db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = TestMessageType,
            Payload = "{}",
            EnqueuedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
        });
        await h.Db.SaveChangesAsync();
        var processedId = (await h.Db.OutboxMessages.AsNoTracking().FirstAsync()).Id;

        var processed = await pq.RequeueAsync(processedId);
        processed.Should().BeFalse("a row already delivered must not be requeued");
    }

    private sealed class ThrowingHandler : IOutboxMessageHandler
    {
        private readonly bool _throws;
        public ThrowingHandler(string type, bool throws) { MessageType = type; _throws = throws; }
        public string MessageType { get; }
        public Task HandleAsync(string payload, CancellationToken cancellationToken)
        {
            if (_throws) throw new InvalidOperationException("simulated failure");
            return Task.CompletedTask;
        }
    }
}
