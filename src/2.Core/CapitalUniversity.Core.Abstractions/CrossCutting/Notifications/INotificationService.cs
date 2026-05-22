using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Notifications
{

    public interface INotificationService
    {
        /// <summary>
        /// Directly persists a notification — synchronous, no outbox. Used by the
        /// outbox handler itself (the consumer side of a "notification.create"
        /// message) and by tests that want a one-shot write without staging.
        ///
        /// <para>
        /// New producer code should prefer <see cref="EnqueueNotificationAsync"/>
        /// so the notification is committed atomically with the surrounding
        /// business state via the transactional outbox.
        /// </para>
        /// </summary>
        Task CreateNotificationAsync(Guid recipientUserId,
            string title,
            string message,
            NotificationType type);

        /// <summary>
        /// Stages a <c>"notification.create"</c> outbox row on the current DbContext.
        /// Does NOT flush — the caller's <c>SaveChangesAsync</c> commits both the
        /// outbox row and any business state in a single transaction. The background
        /// dispatcher drains the row asynchronously and invokes <see cref="CreateNotificationAsync"/>
        /// on the other side; handlers run at-least-once and are idempotent.
        /// </summary>
        Task EnqueueNotificationAsync(Guid recipientUserId,
            string title,
            string message,
            NotificationType type,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Guid userId);

        Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(Guid userId);

        Task MarkAsReadAsync(Guid notificationId, Guid userId);
    }
}

