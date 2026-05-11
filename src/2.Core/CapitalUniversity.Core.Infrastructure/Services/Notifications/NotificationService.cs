using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly CoreDbContext _context;
    private readonly NotificationMapper _mapper;

    public NotificationService(CoreDbContext context)
    {
        _context = context;
        _mapper = new NotificationMapper();
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
