namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IAsyncDisposable
{
    IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(long userId, long sourceId, string token, CancellationToken cancellationToken);

    Task<int> GetTotalFileCountAsync(string token);
}

public interface IDownloadStateService
{
    
}

public interface IProgressReporter
{
}
