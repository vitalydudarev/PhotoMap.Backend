using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

public interface IImageProcessingService
{
    ProcessedImage ProcessImage(DownloadedFileInfo fileInfo, byte[] fileContents, IEnumerable<int> thumbSizes);
}