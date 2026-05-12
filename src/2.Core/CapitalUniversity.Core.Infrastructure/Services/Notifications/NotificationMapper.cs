using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Domain.Notifications;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Infrastructure.Services.Notifications;

[Mapper]
public partial class NotificationMapper
{
    public partial NotificationDto MapToDto(Notification notification);
    public partial IQueryable<NotificationDto> ProjectToDto(IQueryable<Notification> queryable);
}
