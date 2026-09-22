using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace PhotoMap.Api.Hubs;

/// <summary>
/// Clients connect with a userId query parameter and receive the notifications of that user.
/// Notifications are sent through IHubContext&lt;NotificationHub&gt; (see FrontendNotificationService).
/// </summary>
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userIdQueryParameter = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        if (long.TryParse(userIdQueryParameter, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
        }

        await base.OnConnectedAsync();
    }

    public static string GetUserGroupName(long userId)
    {
        return $"user-{userId}";
    }
}
