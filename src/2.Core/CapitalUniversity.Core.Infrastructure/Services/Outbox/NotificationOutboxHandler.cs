using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Infrastructure.Services.Outbox;

/// <summary>
/// Example outbox handler: a producer that wants to atomically commit business
/// state + a follow-up in-app notification enqueues a <c>"notification.create"</c>
/// outbox row with this payload shape. The dispatcher routes it here, which
/// invokes the existing <see cref="INotificationService"/>.
/// </summary>
public class NotificationOutboxHandler : IOutboxMessageHandler
{
    public const string TypeKey = "notification.create";

    private readonly INotificationService _notifications;

    public NotificationOutboxHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public string MessageType => TypeKey;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var msg = JsonSerializer.Deserialize<NotificationPayload>(payload)
                  ?? throw new InvalidOperationException("NotificationOutboxHandler: payload deserialised to null.");

        await _notifications.CreateNotificationAsync(
            msg.RecipientUserId,
            msg.Title,
            msg.Message,
            msg.Type);
    }

    public record NotificationPayload(Guid RecipientUserId, string Title, string Message, NotificationType Type);
}
