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

    public Task SendProcessingAvailabilityAsync(bool available, string? reason)
    {
        return _hubContext.Clients.All.SendAsync("ProcessingAvailability", new HubProcessingAvailabilityModel(available, reason));
    }

    public Task SendProgressAsync(UserPhotoSourceStatus status)
    {
        return SendProgressAsync("Progress", status);
    }

    public Task SendProgressAsync(UserPhotoSourceStatus status, RetryProgress retry)
    {
        return SendProgressAsync("Progress", status, new HubRetryModel(retry.Total, retry.Retried, retry.Recovered));
    }

    public Task SendVideoErrorAsync(long userId, long sourceId, string errorText)
    {
        var hubErrorModel = new HubErrorModel(sourceId, errorText);

        return _hubContext.Clients.Group(NotificationHub.GetUserGroupName(userId)).SendAsync("VideoError", hubErrorModel);
    }

    public Task SendVideoProgressAsync(UserPhotoSourceStatus status)
    {
        return SendProgressAsync("VideoProgress", status);
    }

    public Task SendOtherUploadsErrorAsync(long userId, long sourceId, string errorText)
    {
        var hubErrorModel = new HubErrorModel(sourceId, errorText);

        return _hubContext.Clients.Group(NotificationHub.GetUserGroupName(userId)).SendAsync("OtherUploadsError", hubErrorModel);
    }

    public Task SendOtherUploadsProgressAsync(UserPhotoSourceStatus status)
    {
        return SendProgressAsync("OtherUploadsProgress", status);
    }

    public Task SendOtherUploadsProgressAsync(UserPhotoSourceStatus status, RetryProgress retry)
    {
        return SendProgressAsync("OtherUploadsProgress", status, new HubRetryModel(retry.Total, retry.Retried, retry.Recovered));
    }

    public Task SendDownloadErrorAsync(long userId, long sourceId, SourceDownloadKind kind, string errorText)
    {
        var hubErrorModel = new HubErrorModel(sourceId, errorText);
        var method = kind == SourceDownloadKind.ChosenFolder ? "FolderDownloadError" : "DownloadError";

        return _hubContext.Clients.Group(NotificationHub.GetUserGroupName(userId)).SendAsync(method, hubErrorModel);
    }

    public Task SendDownloadProgressAsync(UserPhotoSourceStatus status, SourceDownloadKind kind)
    {
        return SendProgressAsync(kind == SourceDownloadKind.ChosenFolder ? "FolderDownloadProgress" : "DownloadProgress", status);
    }

    private Task SendProgressAsync(string method, UserPhotoSourceStatus status, HubRetryModel? retry = null)
    {
        var hubProgressModel = new HubProgressModel(status.PhotoSourceId, status.Status.ToString(), status.ProcessedCount,
            status.FailedCount, status.TotalCount) { Retry = retry };

        return _hubContext.Clients.Group(NotificationHub.GetUserGroupName(status.UserId)).SendAsync(method, hubProgressModel);
    }
}
