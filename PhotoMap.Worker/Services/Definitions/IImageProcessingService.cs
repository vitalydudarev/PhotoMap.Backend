using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services.Definitions;

public interface IImageProcessingService
{
    ProcessedImage ProcessImage(ProcessImageRequest request);
}
