using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services;

public interface IPhotoSourceProcessingService
{
    Task ProcessAsync(long userId, long sourceId, PhotoSourceProcessingCommands command);
}