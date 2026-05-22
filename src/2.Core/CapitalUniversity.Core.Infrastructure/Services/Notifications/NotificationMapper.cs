using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Domain.Notifications;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Infrastructure.Services.Notifications;

// RequiredMappingStrategy.None: NotificationDto carries forward-compat
// fields (ReferenceId) that aren't yet on the entity, and the entity has
// audit/soft-delete plumbing that doesn't belong on client payloads.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class NotificationMapper
{
    public partial NotificationDto MapToDto(Notification notification);
    public partial IQueryable<NotificationDto> ProjectToDto(IQueryable<Notification> queryable);
}
