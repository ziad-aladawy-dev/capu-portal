using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
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
        /// <param name="idempotencyKey">H5 — when supplied, the call is a no-op
        /// on collision with an existing row carrying the same key. Outbox
        /// handlers pass the OutboxMessage.Id so at-least-once redelivery
        /// does not produce duplicate notifications.</param>
        Task CreateNotificationAsync(Guid recipientUserId,
            string title,
            string message,
            NotificationType type,
            Guid? idempotencyKey = null,
            CancellationToken cancellationToken = default);

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

        /// <summary>
        /// Marks every notification in <paramref name="notificationIds"/> as read
        /// for the given recipient. Ids that don't exist or belong to another user
        /// are silently skipped (no existence leak). Already-read rows are a
        /// no-op. Returns the count of rows actually transitioned from unread to
        /// read on this call.
        /// </summary>
        Task<int> MarkManyAsReadAsync(IReadOnlyList<Guid> notificationIds, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks every unread notification belonging to <paramref name="userId"/>
        /// as read. Returns the count of rows transitioned on this call.
        /// </summary>
        Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Paged inbox query with filters (read state, type, date range).
        /// Scope is always the caller's own notifications — no cross-user reads.
        /// </summary>
        Task<PagedResult<NotificationDto>> SearchAsync(Guid userId, NotificationSearchQuery query, CancellationToken cancellationToken = default);
    }
}

