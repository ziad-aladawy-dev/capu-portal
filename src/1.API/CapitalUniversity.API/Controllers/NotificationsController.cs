using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;

namespace CapitalUniversity.API.Controllers;

[ApiController]
// Explicit lowercase route (was "api/[controller]" → "api/Notifications"). ASP.NET
// routing is case-insensitive so the SPA's "/api/notifications" already matched on
// every OS, but this aligns with the kebab/lowercase convention used by every other
// controller and removes the lone [controller]-token outlier.
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications()
    {
        // L7 — single user-id extraction helper, used here and by any
        // future endpoint that needs the caller's id off the JWT.
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notifications = await _notificationService.GetUserNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _notificationService.MarkAsReadAsync(id, userId);
        return NoContent();
    }

    /// <summary>
    /// Bulk-marks the supplied notifications as read for the caller. Ids that
    /// don't exist or belong to another user are silently skipped. Already-read
    /// rows are a no-op. The response counts only rows actually transitioned on
    /// this call — replaying the same request returns <c>marked: 0</c>.
    /// </summary>
    [HttpPut("read")]
    public async Task<IActionResult> MarkManyAsRead([FromBody] BulkActionRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        // Throws ValidationException with a localized key on empty / oversize.
        // GlobalExceptionHandler maps to 400 ProblemDetails with the standard
        // errors dictionary — no hardcoded English in the response payload.
        BulkRequestGuard.EnsureValidIds(request?.Ids);

        var marked = await _notificationService.MarkManyAsReadAsync(request!.Ids, userId, cancellationToken);
        return Ok(new { Marked = marked });
    }

    /// <summary>
    /// Marks every unread notification for the caller as read. Idempotent — a
    /// repeat call returns <c>marked: 0</c>.
    /// </summary>
    [HttpPut("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var marked = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new { Marked = marked });
    }

    /// <summary>
    /// Paged inbox with filters (<c>isRead</c>, <c>type</c>, <c>from/to</c>
    /// dates). Always scoped to the caller. Default sort: <c>createdAt:desc</c>.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] NotificationSearchQuery query, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _notificationService.SearchAsync(userId, query, cancellationToken);
        return Ok(result);
    }
}
