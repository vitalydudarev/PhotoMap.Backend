using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Hubs;
using PhotoMap.Api.Hubs.Models;

namespace PhotoMap.Api;

public class FrontendNotificationService : IFrontendNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public FrontendNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendErrorAsync(long userId, long sourceId, string errorText)
    {
        var hubErrorModel = new HubErrorModel(sourceId, errorText);

        return _hubContext.Clients.Group(NotificationHub.GetUserGroupName(userId)).SendAsync("Error", hubErrorModel);
    }

    public Task SendProgressAsync(UserPhotoSourceStatus status)
    {
        var hubProgressModel = new HubProgressModel(status.PhotoSourceId, status.Status.ToString(), status.ProcessedCount,
            status.FailedCount, status.TotalCount);

        return _hubContext.Clients.Group(NotificationHub.GetUserGroupName(status.UserId)).SendAsync("Progress", hubProgressModel);
    }
}
