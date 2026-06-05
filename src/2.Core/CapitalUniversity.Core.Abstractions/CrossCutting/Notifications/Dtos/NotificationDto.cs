using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

