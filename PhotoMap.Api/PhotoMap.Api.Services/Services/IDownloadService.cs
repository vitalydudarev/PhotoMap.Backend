namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IAsyncDisposable
{
    IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(long userId, long sourceId, string token, CancellationToken cancellationToken);

    Task<int> GetTotalFileCountAsync();
}

public interface IDownloadStateService
{
    
}

public interface IProgressReporter
{
}
