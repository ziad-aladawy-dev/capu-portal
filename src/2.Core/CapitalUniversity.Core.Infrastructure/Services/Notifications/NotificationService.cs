using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly CoreDbContext _context;
    private readonly NotificationMapper _mapper;
    private readonly IOutbox? _outbox;

    public NotificationService(CoreDbContext context, IOutbox? outbox = null)
    {
        _context = context;
        _mapper = new NotificationMapper();
        _outbox = outbox;
    }

    public async Task CreateNotificationAsync(Guid recipientUserId, string title, string message, NotificationType type)
    {
        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    public Task EnqueueNotificationAsync(Guid recipientUserId, string title, string message, NotificationType type, CancellationToken cancellationToken = default)
    {
        if (_outbox is null)
        {
            throw new InvalidOperationException(
                "EnqueueNotificationAsync requires IOutbox to be registered. " +
                "Verify AddCoreServices was called — the outbox + dispatcher are wired there.");
        }

        var payload = new NotificationOutboxHandler.NotificationPayload(
            recipientUserId, title, message, type);

        // Outbox stages the row on this DbContext; the caller's SaveChangesAsync
        // commits the notification atomically with whatever business state they're
        // saving alongside it.
        return _outbox.EnqueueAsync(NotificationOutboxHandler.TypeKey, payload, cancellationToken);
    }


    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notifications.Select(n => _mapper.MapToDto(n));
    }

    public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notifications.Select(n => _mapper.MapToDto(n));
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);

        if (notification != null && notification.RecipientUserId == userId && !notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }
}

