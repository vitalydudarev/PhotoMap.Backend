using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IAsyncDisposable
{
    IAsyncEnumerable<DownloadedFile> DownloadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a single file of the source, identified by the ID the source assigned to it or by its path.
    /// </summary>
    Task<byte[]> DownloadFileAsync(string fileReference, CancellationToken cancellationToken);

    Task<int> GetTotalFileCountAsync();
}

public interface IDownloadStateService
{
    
}

public interface IProgressReporter
{
}
