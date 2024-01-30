using PhotoMap.Shared.Models;
using PhotoMap.Worker.Models;

namespace PhotoMap.Worker.Services.Definitions
{
    public interface IImageProcessingServiceOld
    {
        Task<ProcessedDownloadedFile> ProcessImageAsync(DownloadedFileInfo downloadedFile);
    }
}
