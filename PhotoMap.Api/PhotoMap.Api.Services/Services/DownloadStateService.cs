using System.Text.Json;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Keeps the state serialized to JSON with the photo source of the user.
/// </summary>
public class DownloadStateService<TState> : IDownloadStateService<TState> where TState : class
{
    private readonly IUserPhotoSourceService _userPhotoSourceService;

    public DownloadStateService(IUserPhotoSourceService userPhotoSourceService)
    {
        _userPhotoSourceService = userPhotoSourceService;
    }

    public async Task<TState?> GetStateAsync(long userId, long sourceId)
    {
        var sourceState = await _userPhotoSourceService.GetUserPhotoStateAsync(userId, sourceId);
        if (sourceState?.State == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TState>(sourceState.State);
    }

    public Task SaveStateAsync(long userId, long sourceId, TState state)
    {
        var stateString = JsonSerializer.Serialize(state);

        return _userPhotoSourceService.UpdateUserPhotoStateAsync(userId, sourceId, stateString);
    }
}
