namespace PhotoMap.Api.Services.Services;

public interface IDropboxDownloadStateService
{
    Task<DropboxDownloadState?> GetStateAsync(long userId, long sourceId);
    Task SaveStateAsync(long userId, long sourceId, DropboxDownloadState state);
}
