namespace PhotoMap.Api.Services;

public interface IPhotoSourceDataService
{
    /// <returns>false if the photo source is being processed, its data is only deleted between runs.</returns>
    Task<bool> DeleteDataAsync(long userId, long sourceId);
}
