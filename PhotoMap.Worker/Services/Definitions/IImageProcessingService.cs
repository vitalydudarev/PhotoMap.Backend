using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services.Definitions;

public interface IImageProcessingService
{
    ProcessedImage ProcessImage(DownloadedFileInfo fileInfo, string fileName, IEnumerable<int> thumbSizes);
}