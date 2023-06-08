namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IDisposable
{
    IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(
        long userId,
        string token,
        CancellationToken cancellationToken);
}

public interface IDownloadStateService
{
    
}

public interface IProgressReporter
{
}
