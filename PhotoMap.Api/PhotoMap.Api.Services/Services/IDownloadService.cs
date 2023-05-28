using Microsoft.Extensions.Logging;
using PhotoMap.Shared;

namespace PhotoMap.Api.Services.Services;

public interface IDownloadService
{
    IAsyncEnumerable<DownloadedFileInfo?> DownloadAsync(
        IUserIdentifier userIdentifier,
        string apiToken,
        StopDownloadAction stoppingAction,
        CancellationToken cancellationToken);
}

public interface IDownloadStateService
{
    
}

public interface IDropboxDownloadStateService
{
    DropboxDownloadState? GetState(long userId);
    void SaveState(DropboxDownloadState state);
}

public interface IProgressReporter
{
}

public class YandexDiskDownloadService : IDownloadService
{
    public YandexDiskDownloadService(
        ILogger<YandexDiskDownloadService> logger,
        IDownloadStateService stateService,
        IProgressReporter progressReporter,
        YandexDiskSettings settings)
    {
    }
    
    public IAsyncEnumerable<DownloadedFileInfo?> DownloadAsync(IUserIdentifier userIdentifier, string apiToken, StopDownloadAction stoppingAction,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}



public class StopDownloadAction
{
    public bool IsStopRequested { get; set; }
}

public class DownloadedFileInfo
{
    public string ResourceName { get; set; }
    public string Path { get; set; }
    public DateTime? CreatedOn { get; set; }
    public byte[] FileContents { get; set; }
    public string FileId { get; set; }

    public DownloadedFileInfo(string resourceName, string path, DateTime? createdOn, byte[] fileContents, string fileId)
    {
        ResourceName = resourceName;
        Path = path;
        CreatedOn = createdOn;
        FileContents = fileContents;
        FileId = fileId;
    }
}