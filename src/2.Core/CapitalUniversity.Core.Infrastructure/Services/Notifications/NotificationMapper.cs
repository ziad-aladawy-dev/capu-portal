using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Domain.Notifications;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Infrastructure.Services.Notifications;

// RequiredMappingStrategy.None: the entity has audit/soft-delete plumbing
// that doesn't belong on client payloads, so not every source member maps.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class NotificationMapper
{
    public partial NotificationDto MapToDto(Notification notification);
    public partial IQueryable<NotificationDto> ProjectToDto(IQueryable<Notification> queryable);
}
