namespace PhotoMap.Api.Services;

public interface IPhotoSourceDataService
{
    /// <returns>false if the photo source is being processed, its data is only deleted between runs.</returns>
    Task<bool> DeleteDataAsync(long userId, long sourceId);

    /// <summary>
    /// Deletes the data of every photo source of every user, as <see cref="DeleteDataAsync"/> does for one.
    /// </summary>
    /// <returns>false if any photo source is being processed, nothing is deleted then.</returns>
    Task<bool> DeleteAllDataAsync();
}
