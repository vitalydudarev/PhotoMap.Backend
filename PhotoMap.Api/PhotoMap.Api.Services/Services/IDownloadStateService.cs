namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The state a run of a photo source saves as it goes, to resume from when it is stopped.
/// </summary>
public interface IDownloadStateService<TState> where TState : class
{
    Task<TState?> GetStateAsync(long userId, long sourceId);
    Task SaveStateAsync(long userId, long sourceId, TState state);
}
