using Microsoft.Extensions.Logging;
using PhotoMap.Shared;

namespace PhotoMap.Api.Services.Services;

public interface IDownloadService : IDisposable
{
    IAsyncEnumerable<DownloadedFileInfo?> DownloadAsync(
        long userId,
        string token,
        StopDownloadAction stoppingAction,
        CancellationToken cancellationToken);
}

public interface IDownloadStateService
{
    
}



public interface IProgressReporter
{
}







