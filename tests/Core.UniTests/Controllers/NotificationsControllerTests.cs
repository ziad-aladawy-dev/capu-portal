using System.Security.Claims;
using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class NotificationsControllerTests
{
    private static NotificationsController NewController(
        Guid? userId,
        out Mock<INotificationService> svc)
    {
        svc = new Mock<INotificationService>(MockBehavior.Strict);
        var ctrl = new NotificationsController(svc.Object);
        var http = new DefaultHttpContext();
        if (userId.HasValue)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
            }));
        }
        ctrl.ControllerContext = new ControllerContext { HttpContext = http };
        return ctrl;
    }

    [Fact]
    public async Task GetUserNotifications_NoClaim_ReturnsUnauthorized()
    {
        var ctrl = NewController(null, out _);
        var result = await ctrl.GetUserNotifications();
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetUserNotifications_ValidClaim_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var ctrl = NewController(userId, out var svc);
        var notifs = (IEnumerable<NotificationDto>)new List<NotificationDto> { new() };
        svc.Setup(s => s.GetUserNotificationsAsync(userId)).ReturnsAsync(notifs);

        var result = await ctrl.GetUserNotifications();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(notifs, ok.Value);
    }

    [Fact]
    public async Task GetUnreadNotifications_NoClaim_ReturnsUnauthorized()
    {
        var ctrl = NewController(null, out _);
        var result = await ctrl.GetUnreadNotifications();
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetUnreadNotifications_ValidClaim_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var ctrl = NewController(userId, out var svc);
        var notifs = (IEnumerable<NotificationDto>)new List<NotificationDto> { new() };
        svc.Setup(s => s.GetUnreadNotificationsAsync(userId)).ReturnsAsync(notifs);

        var result = await ctrl.GetUnreadNotifications();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(notifs, ok.Value);
    }

    [Fact]
    public async Task MarkAsRead_NoClaim_ReturnsUnauthorized()
    {
        var ctrl = NewController(null, out _);
        var result = await ctrl.MarkAsRead(Guid.NewGuid());
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task MarkAsRead_ValidClaim_DelegatesAndReturnsNoContent()
    {
        var userId = Guid.NewGuid();
        var ctrl = NewController(userId, out var svc);
        var notifId = Guid.NewGuid();
        svc.Setup(s => s.MarkAsReadAsync(notifId, userId)).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.MarkAsRead(notifId);

        Assert.IsType<NoContentResult>(result);
        svc.Verify();
    }
}