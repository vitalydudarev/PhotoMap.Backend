namespace PhotoMap.Api.Services.Services;

public interface IDropboxDownloadStateService
{
    DropboxDownloadState? GetState(long userId);
    void SaveState(DropboxDownloadState state);
}
