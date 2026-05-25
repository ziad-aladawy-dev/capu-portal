using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;

/// <summary>
/// Filters / pagination / sort for the caller's notification inbox. The user
/// id is bound from the JWT in the controller, not on the wire.
/// </summary>
/// <remarks>
/// Sort whitelist: <c>createdAt</c>. Default order: <c>createdAt:desc</c>.
/// Date params: <c>From</c> inclusive, <c>To</c> exclusive.
/// </remarks>
public class NotificationSearchQuery : PagedQueryRequest
{
    public bool? IsRead { get; set; }
    public NotificationType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
