using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services.Definitions;

public interface IImageProcessingService
{
    ProcessedImage ProcessImage(DownloadedFileInfo downloadedFile, IEnumerable<int> thumbSizes);
}