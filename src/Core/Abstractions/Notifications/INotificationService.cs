using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Abstractions.Notifications.Dtos;

namespace CapitalUniversity.Core.Abstractions.Notifications;

public interface INotificationService
{
    Task CreateNotificationAsync(
        Guid recipientUserId,
        string title,
        string message,
        NotificationType type,
        Guid? referenceId = null,
        string? referenceType = null);

    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Guid userId);

    Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(Guid userId);

    Task MarkAsReadAsync(Guid notificationId, Guid userId);
}
