using System.Text.Json;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

public class DropboxDownloadStateService : IDropboxDownloadStateService
{
    private readonly IUserPhotoSourceService _userPhotoSourceService;

    public DropboxDownloadStateService(IUserPhotoSourceService userPhotoSourceService)
    {
        _userPhotoSourceService = userPhotoSourceService;
    }

    public async Task<DropboxDownloadState?> GetStateAsync(long userId, long sourceId)
    {
        var sourceState = await _userPhotoSourceService.GetUserPhotoStateAsync(userId, sourceId);
        if (sourceState?.State == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<DropboxDownloadState>(sourceState.State);
    }

    public Task SaveStateAsync(long userId, long sourceId, DropboxDownloadState state)
    {
        var stateString = JsonSerializer.Serialize(state);

        return _userPhotoSourceService.UpdateUserPhotoStateAsync(userId, sourceId, stateString);
    }
}