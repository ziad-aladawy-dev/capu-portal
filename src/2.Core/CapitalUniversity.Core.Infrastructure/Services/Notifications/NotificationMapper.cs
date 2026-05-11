using System.Linq;
using Riok.Mapperly.Abstractions;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;

namespace CapitalUniversity.Core.Application.Notifications;

[Mapper]
public partial class NotificationMapper
{
    public partial NotificationDto MapToDto(Notification notification);
    public partial IQueryable<NotificationDto> ProjectToDto(IQueryable<Notification> queryable);
}
