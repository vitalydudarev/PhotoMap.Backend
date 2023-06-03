using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests.Stubs;

public class DownloadServiceStub : IDownloadService
{
    public IAsyncEnumerable<DownloadedFileInfo?> DownloadAsync(long userId, string token, StopDownloadAction stoppingAction,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}