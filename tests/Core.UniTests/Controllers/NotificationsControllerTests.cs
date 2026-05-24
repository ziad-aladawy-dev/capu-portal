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

    // Task 1 cleanup — three near-duplicate "no claim → 401" tests folded
    // into a single Theory. Same invariant (every action 401s when no user-id
    // claim is present), so duplicating the scenario per endpoint added
    // mutation-score noise without exercising different code.
    public static IEnumerable<object[]> NoClaimActions()
    {
        yield return new object[] { "GetUserNotifications" };
        yield return new object[] { "GetUnreadNotifications" };
        yield return new object[] { "MarkAsRead" };
    }

    [Theory]
    [MemberData(nameof(NoClaimActions))]
    public async Task Action_WithoutUserIdClaim_ReturnsUnauthorized(string action)
    {
        var ctrl = NewController(null, out _);

        IActionResult result = action switch
        {
            "GetUserNotifications" => await ctrl.GetUserNotifications(),
            "GetUnreadNotifications" => await ctrl.GetUnreadNotifications(),
            "MarkAsRead" => await ctrl.MarkAsRead(Guid.NewGuid()),
            _ => throw new InvalidOperationException($"Unknown action: {action}")
        };

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
