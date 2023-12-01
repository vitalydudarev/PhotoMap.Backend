using System.Threading.Tasks;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Hubs;

namespace PhotoMap.Api;

public class FrontendNotificationService : IFrontendNotificationService
{
    private readonly NotificationHub _hub;

    public FrontendNotificationService(NotificationHub hub)
    {
        _hub = hub;
    }

    public async Task SendErrorAsync(long userId, long sourceId, string errorText)
    {
        await _hub.SendErrorAsync(userId, sourceId, errorText);
    }

    public async Task SendProgressAsync(long userId, long sourceId, int processed, int total)
    {
        await _hub.SendProgressAsync(userId, sourceId, processed, total);
    }
}