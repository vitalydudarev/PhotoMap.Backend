using PhotoMap.Shared;
using PhotoMap.Worker.Models;

namespace PhotoMap.Worker.Services.Definitions
{
    public interface IDropboxDownloadService
    {
        IAsyncEnumerable<DropboxFileInfo?> DownloadAsync(
            IUserIdentifier userIdentifier,
            string apiToken,
            StoppingAction stoppingAction,
            CancellationToken cancellationToken);
    }
}
