using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Hubs;

[Authorize]
public class StudentServicesHub : Hub
{
    public async Task JoinRequestGroup(Guid requestId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"request-{requestId}");
    }

    public async Task LeaveRequestGroup(Guid requestId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"request-{requestId}");
    }

    public async Task NotifyStaffNewRequest(Guid requestId, string serviceName)
    {
        await Clients.Group("staff-notifications").SendAsync("NewRequestReceived", new { requestId, serviceName });
    }

    public async Task JoinStaffGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "staff-notifications");
    }

    public async Task LeaveStaffGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff-notifications");
    }
}