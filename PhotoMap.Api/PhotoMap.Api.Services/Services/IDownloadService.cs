using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IAsyncDisposable
{
    IAsyncEnumerable<DownloadedFile> DownloadAsync(CancellationToken cancellationToken);

    Task<int> GetTotalFileCountAsync();
}

public interface IDownloadStateService
{
    
}

public interface IProgressReporter
{
}
