using Microsoft.Extensions.Logging;

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
    
    public IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(long userId, long sourceId, string token,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<int> GetTotalFileCountAsync()
    {
        throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        // TODO release managed resources here
    }
}