using PhotoMap.Shared;
using PhotoMap.Worker.Models;

namespace PhotoMap.Worker.Services.Definitions
{
    public interface IYandexDiskDownloadService
    {
        IAsyncEnumerable<YandexDiskFileInfo> DownloadFilesAsync(
            IUserIdentifier userIdentifier,
            string accessToken,
            CancellationToken cancellationToken,
            StoppingAction stoppingAction);
    }
}
