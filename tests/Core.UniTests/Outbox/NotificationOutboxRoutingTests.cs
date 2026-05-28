using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using Moq;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Notifications;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Outbox;

/// <summary>
/// C1 contract: <c>EnqueueNotificationAsync</c> stages an outbox row of the right
/// type + payload shape but DOES NOT flush. The caller's <c>SaveChangesAsync</c>
/// makes the row durable in the same transaction as their business state.
/// </summary>
public class NotificationOutboxRoutingTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("NotifOutbox_" + Guid.NewGuid())
            .Options);

    private static OutboxService BuildOutbox(CoreDbContext db)
    {
        return new OutboxService(
            db, 
            new Mock<IExecutionContext>().Object, 
            new Mock<ICurrentCultureService>().Object);
    }

    [Fact]
    public async Task EnqueueNotificationAsync_StagesOutboxRow_WithoutFlushing()
    {
        using var db = NewDb();
        var outbox = BuildOutbox(db);
        var sut = new NotificationService(db, new TestLocalizationService(), outbox);

        var recipient = Guid.NewGuid();
        await sut.EnqueueNotificationAsync(recipient, "Hi", "msg", NotificationType.Info);

        // Until SaveChangesAsync runs, the row is staged on the change tracker only —
        // no notification, no committed outbox row.
        (await db.Notifications.CountAsync()).Should().Be(0);
        db.ChangeTracker.Entries<CapitalUniversity.Core.Domain.Outbox.OutboxMessage>()
            .Should().HaveCount(1, "the outbox row must be staged but not yet flushed");

        await db.SaveChangesAsync();

        var row = await db.OutboxMessages.AsNoTracking().SingleAsync();
        row.MessageType.Should().Be(NotificationOutboxHandler.TypeKey);
        row.ProcessedAt.Should().BeNull("dispatcher hasn't run yet");

        // Payload round-trip — handler must be able to deserialise back.
        var payload = JsonSerializer.Deserialize<NotificationOutboxHandler.NotificationPayload>(row.Payload);
        payload.Should().NotBeNull();
        payload!.RecipientUserId.Should().Be(recipient);
        
        // Assert on the normalized JSON identity, not verbatim equality.
        LocalizedJson.Extract(payload.Title, "en").Should().Be("Hi");
        LocalizedJson.Extract(payload.Message, "en").Should().Be("msg");
        payload.Type.Should().Be(NotificationType.Info);
    }

    [Fact]
    public async Task EnqueueNotificationAsync_NoOutboxRegistered_Throws()
    {
        using var db = NewDb();
        var sut = new NotificationService(db, new TestLocalizationService(), outbox: null);

        await FluentActions.Awaiting(() =>
                sut.EnqueueNotificationAsync(Guid.NewGuid(), "t", "m", NotificationType.Info))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outbox*", "missing IOutbox is a wiring bug worth failing loudly on");
    }

    [Fact]
    public async Task EnqueueThenDispatch_EndToEnd_PersistsNotification()
    {
        using var db = NewDb();
        var outbox = BuildOutbox(db);
        var notifications = new NotificationService(db, new TestLocalizationService(), outbox);

        var recipient = Guid.NewGuid();
        await notifications.EnqueueNotificationAsync(recipient, "Welcome", "Glad you're here", NotificationType.Info);
        await db.SaveChangesAsync();

        // Simulate the dispatcher: pull the row, invoke the handler.
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync();
        var handler = new NotificationOutboxHandler(notifications);
        await handler.HandleAsync(row.Id, row.Payload, CancellationToken.None);

        var stored = await db.Notifications.AsNoTracking().SingleAsync();
        stored.RecipientUserId.Should().Be(recipient);
        LocalizedJson.Extract(stored.Title, "en").Should().Be("Welcome");
        LocalizedJson.Extract(stored.Message, "en").Should().Be("Glad you're here");
        stored.IsRead.Should().BeFalse();
    }
}
