namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IDisposable
{
    IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(
        long userId,
        string token,
        CancellationToken cancellationToken);

    Task<int> GetTotalFileCountAsync();
}

public interface IDownloadStateService
{
    
}

public interface IProgressReporter
{
}
