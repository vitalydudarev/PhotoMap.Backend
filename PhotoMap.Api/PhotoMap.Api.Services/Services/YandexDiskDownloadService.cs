using Microsoft.Extensions.Logging;
using PhotoMap.Shared;

namespace PhotoMap.Api.Services.Services;

public class YandexDiskDownloadService : IDownloadService
{
    public YandexDiskDownloadService(
        ILogger<YandexDiskDownloadService> logger,
        IDownloadStateService stateService,
        IProgressReporter progressReporter,
        YandexDiskSettings settings)
    {
    }
    
    public IAsyncEnumerable<DownloadedFileInfo?> DownloadAsync(long userId, string token, StopDownloadAction stoppingAction,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}