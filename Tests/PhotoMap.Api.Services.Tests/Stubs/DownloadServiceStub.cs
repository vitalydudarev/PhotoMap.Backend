using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests.Stubs;

public class DownloadServiceStub : IDownloadService
{
    public IAsyncEnumerable<DownloadedFileInfo> DownloadAsync(CancellationToken cancellationToken)
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