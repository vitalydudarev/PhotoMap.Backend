using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IAsyncDisposable
{
    IAsyncEnumerable<DownloadedFile> DownloadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a single file of the source, identified by the ID the source assigned to it or by its path,
    /// whichever the source downloads files by. At least one of them is given.
    /// </summary>
    Task<byte[]> DownloadFileAsync(string? externalId, string? path, CancellationToken cancellationToken);

    Task<int> GetTotalFileCountAsync();
}

public interface IProgressReporter
{
}
