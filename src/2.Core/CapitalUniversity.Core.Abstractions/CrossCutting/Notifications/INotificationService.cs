using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.Common;
=======
using CapitalUniversity.Core.Domain.Shared;
>>>>>>> Stashed changes

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Notifications
{

    public interface INotificationService
    {
        Task CreateNotificationAsync(Guid recipientUserId,
            string title,
            string message,
            NotificationType type);

        Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Guid userId);

        Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(Guid userId);

        Task MarkAsReadAsync(Guid notificationId, Guid userId);
    }
}

