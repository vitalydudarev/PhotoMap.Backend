namespace PhotoMap.Api.Services.Services;

public interface IYandexDiskDownloadStateService
{
    Task<YandexDiskDownloadState?> GetStateAsync(long userId, long sourceId);
    Task SaveStateAsync(long userId, long sourceId, YandexDiskDownloadState state);
}
