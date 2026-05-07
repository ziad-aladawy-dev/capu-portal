using System;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Notifications;

public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
