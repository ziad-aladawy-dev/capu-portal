using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Shared;

namespace CapitalUniversity.Core.Domain.Notifications;

public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
}
