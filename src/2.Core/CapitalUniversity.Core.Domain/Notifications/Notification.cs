using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Notifications;

public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }

    // H5 — When this notification was created by the outbox dispatcher, this
    // is the originating OutboxMessage.Id. A filtered unique index on this
    // column makes a replayed handler attempt fail at the DB layer instead
    // of inserting a duplicate row. Direct callers (CreateNotificationAsync
    // without an idempotency key) leave it null and bypass the dedup, which
    // is what we want for one-off, caller-driven creates.
    public Guid? IdempotencyKey { get; set; }
}
