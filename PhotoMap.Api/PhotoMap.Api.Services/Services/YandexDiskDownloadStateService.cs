using System.Text.Json;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

public class YandexDiskDownloadStateService : IYandexDiskDownloadStateService
{
    private readonly IUserPhotoSourceService _userPhotoSourceService;

    public YandexDiskDownloadStateService(IUserPhotoSourceService userPhotoSourceService)
    {
        _userPhotoSourceService = userPhotoSourceService;
    }

    public async Task<YandexDiskDownloadState?> GetStateAsync(long userId, long sourceId)
    {
        var sourceState = await _userPhotoSourceService.GetUserPhotoStateAsync(userId, sourceId);
        if (sourceState?.State == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<YandexDiskDownloadState>(sourceState.State);
    }

    public Task SaveStateAsync(long userId, long sourceId, YandexDiskDownloadState state)
    {
        var stateString = JsonSerializer.Serialize(state);

        return _userPhotoSourceService.UpdateUserPhotoStateAsync(userId, sourceId, stateString);
    }
}
